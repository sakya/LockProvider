using System.Text.RegularExpressions;

namespace LockProvider;

public class LockProvider : IAsyncDisposable
{
    public enum LockLogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    #region classes
    public class SemaphoreInfo
    {
        public SemaphoreInfo(string owner, string name, DateTime acquiredAt, DateTime? expiresAt)
        {
            Owner = owner;
            Name = name;
            AcquiredAt = acquiredAt;
            ExpiresAt = expiresAt;
        }

        public string Owner { get; set; }
        public string Name { get; set; }
        public DateTime AcquiredAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    private class SemaphoreInfoExtended : SemaphoreInfo
    {
        private readonly FifoSemaphore _semaphore;

        public SemaphoreInfoExtended(string owner, string name, FifoSemaphore semaphore, DateTime? expiresAt = null) :
            base(owner, name, DateTime.UtcNow, expiresAt)
        {
            _semaphore = semaphore;
        }

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await _semaphore.WaitAsync(timeout, cancellationToken);
        }

        public void Release()
        {
            _semaphore.Release();
        }

        public static string GetKey(string owner, string name)
        {
            return $"{owner}:{name}";
        }
    }

    #endregion

    #region delegates
    public delegate void LogDelegate(LockLogLevel level, string message);
    public LogDelegate? Log { get; set; }
    #endregion

    private bool _disposed;
    private readonly FifoSemaphore _mainLock = new(1, 1);
    private readonly Dictionary<string, SemaphoreInfoExtended> _locks = new();

    private readonly FifoSemaphore _waitingLocksLock = new(1, 1);
    private readonly Dictionary<string, int> _waitingLocks = new();

    private readonly CancellationTokenSource _expirationTaskCts = new();
    private readonly Task _expirationTask;

    public LockProvider()
    {
        _expirationTask = Task.Run(() => ExpireLocksLoop(_expirationTaskCts.Token));
    }

    /// <summary>
    /// Get the count of the current locks
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetLocksCount()
    {
        await _mainLock.WaitAsync();
        try {
            return _locks.Count;
        } finally {
            _mainLock.Release();
        }
    }

    /// <summary>
    /// Get the count of the waiting locks
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetWaitingLocksCount()
    {
        await _waitingLocksLock.WaitAsync();
        try {
            return _waitingLocks.Count;
        } finally {
            _waitingLocksLock.Release();
        }
    }

    /// <summary>
    /// Returns true if a name is locked
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> IsLocked(string owner, string name)
    {
        (owner, name) = Validate(owner, name);
        await _mainLock.WaitAsync();
        try {
            if (_locks.TryGetValue(SemaphoreInfoExtended.GetKey(owner, name), out var semaphore)) {
                return !semaphore.IsExpired;
            }
            return false;
        } finally {
            _mainLock.Release();
        }
    }

    /// <summary>
    /// Acquire a named lock waiting a maximum time
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <param name="timeout">The timeout in seconds</param>
    /// <param name="timeToLive">The lock time to live in seconds. If set to 0 the lock will never expire</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="TimeoutException"></exception>
    public async Task<SemaphoreInfo> AcquireLock(string owner, string name, int timeout, int timeToLive = 0)
    {
        (owner, name) = Validate(owner, name);

        if (timeout <= 0) {
            throw new ArgumentException("Timeout must be greater than zero");
        }

        if (timeToLive < 0) {
            throw new ArgumentException("TimeToLive must be equal or greater than zero");
        }

        var key = SemaphoreInfoExtended.GetKey(owner, name);
        await _waitingLocksLock.WaitAsync();
        if (_waitingLocks.TryGetValue(key, out var count)) {
            _waitingLocks[key] = count + 1;
        } else {
            _waitingLocks[key] = 1;
        }
        _waitingLocksLock.Release();

        SemaphoreInfoExtended? semaphore;
        var createdNew = false;

        await _mainLock.WaitAsync();
        try {
            if (!_locks.TryGetValue(key, out semaphore)) {
                // The initial count is zero, the lock is acquired
                DateTime? expiresAt = null;
                if (timeToLive > 0) {
                    expiresAt = DateTime.UtcNow.AddSeconds(timeToLive);
                }

                semaphore = new SemaphoreInfoExtended(owner, name, new FifoSemaphore(0, 1), expiresAt);
                _locks[key] = semaphore;
                createdNew = true;
            }
        } finally {
            _mainLock.Release();
        }

        if (!createdNew && !await semaphore.WaitAsync(TimeSpan.FromSeconds(timeout))) {
            await RemoveFromWaitingLocks(key);
            throw new TimeoutException($"Lock '{name}' timed out");
        }

        await RemoveFromWaitingLocks(key);

        return new SemaphoreInfo(semaphore.Owner, semaphore.Name, semaphore.AcquiredAt, semaphore.ExpiresAt);
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <returns>True on success, false if the lock was not found</returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> ReleaseLock(string owner, string name)
    {
        return await ReleaseLockPrimitive(owner, name, true);
    }

    /// <summary>
    /// Get a list of locks
    /// </summary>
    /// <param name="owner">The owner</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<List<SemaphoreInfo>> LocksList(string owner)
    {
        return await LocksList(owner, "*");
    }

    /// <summary>
    /// Get a list of locks
    /// </summary>
    /// <param name="owner">The owner</param>
    /// <param name="nameRegex">The name regex</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<List<SemaphoreInfo>> LocksList(string owner, string nameRegex)
    {
        (owner, nameRegex) = Validate(owner, nameRegex, true);
        var res = new List<SemaphoreInfo>();
        await _mainLock.WaitAsync();
        List<SemaphoreInfoExtended> locks;
        try {
            locks = _locks.Values.ToList();
        } finally {
            _mainLock.Release();
        }

        Regex? regex = null;
        if (!string.IsNullOrEmpty(nameRegex)) {
            if (!nameRegex.StartsWith('^'))
                nameRegex = $"^{nameRegex}";
            if (!nameRegex.EndsWith('$'))
                nameRegex = $"{nameRegex}$";

            if (nameRegex != "^*$")
                regex = new Regex(nameRegex);
        }

        foreach (var s in locks) {
            if (s.IsExpired)
                continue;
            if (s.Owner != owner)
                continue;
            if (regex != null && !regex.IsMatch(s.Name))
                continue;

            res.Add(new SemaphoreInfo(s.Owner, s.Name, s.AcquiredAt, s.ExpiresAt));
        }

        return res
            .OrderBy(l => l.AcquiredAt)
            .ToList();
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <param name="skipExpired">If set to true expired lock are not released</param>
    /// <returns>True on success, false if the lock was not found</returns>
    /// <exception cref="ArgumentException"></exception>
    private async Task<bool> ReleaseLockPrimitive(string owner, string name, bool skipExpired)
    {
        (owner, name) = Validate(owner, name);
        var key = SemaphoreInfoExtended.GetKey(owner, name);
        await _mainLock.WaitAsync();
        await _waitingLocksLock.WaitAsync();
        try {
            if (_locks.TryGetValue(key, out var semaphore)) {
                if (skipExpired && semaphore.IsExpired)
                    return false;

                semaphore.Release();

                var isWaiting = _waitingLocks.ContainsKey(key);
                if (!isWaiting) {
                    _locks.Remove(key);
                }
            } else {
                return false;
            }
        } finally {
            _waitingLocksLock.Release();
            _mainLock.Release();
        }

        return true;
    }

    private async Task RemoveFromWaitingLocks(string key)
    {
        await _waitingLocksLock.WaitAsync();
        if (_waitingLocks.TryGetValue(key, out var count)) {
            if (count <= 1) {
                _waitingLocks.Remove(key);
            } else {
                _waitingLocks[key] = count - 1;
            }
        }
        _waitingLocksLock.Release();
    }

    private async Task ExpireLocksLoop(CancellationToken token)
    {
        try {
            while (!token.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(1), token);

                await _mainLock.WaitAsync(token);
                List<SemaphoreInfo> expiredLocks;
                try {
                    var now = DateTime.UtcNow;
                    expiredLocks = _locks
                        .Where(kvp => kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt <= now)
                        .Select(kvp => new SemaphoreInfo(kvp.Value.Owner, kvp.Value.Name, kvp.Value.AcquiredAt, kvp.Value.ExpiresAt))
                        .ToList();
                } finally {
                    _mainLock.Release();
                }

                foreach (var l in expiredLocks) {
                    if (token.IsCancellationRequested)
                        break;

                    try {
                        await ReleaseLockPrimitive(l.Owner, l.Name, false);
                        Log?.Invoke(LockLogLevel.Info, $"Lock {l.Name} ({l.Owner}) expired");
                    } catch (Exception ex) {
                        // Ignored because the lock could have been released in the meantime
                        Log?.Invoke(LockLogLevel.Warning,
                            $"Failed to release expired lock {l.Name} ({l.Owner}): {ex.Message}");
                    }
                }
            }
        } catch (OperationCanceledException) {
            // Ignored
        } catch (Exception ex) {
            Log?.Invoke(LockLogLevel.Warning, $"ExpireLocksLoop error: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await DisposeAsync(true);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsync(bool disposing)
    {
        if (disposing) {
            await _expirationTaskCts.CancelAsync();
            await _expirationTask;
            _expirationTaskCts.Dispose();

            await _mainLock.WaitAsync();
            try {
                foreach (var kvp in _locks) {
                    kvp.Value.Release();
                }

                _locks.Clear();
            } finally {
                _mainLock.Release();
            }
        }
    }

    private static (string, string) Validate(string owner, string name, bool isNameRegex = false)
    {
        owner = owner.Trim();
        if (string.IsNullOrEmpty(owner)) {
            throw new ArgumentException("Owner cannot be empty");
        }

        if (!isNameRegex) {
            name = name.Trim();
            if (string.IsNullOrEmpty(name)) {
                throw new ArgumentException("Name cannot be empty");
            }
        }

        return (owner, name);
    }
}