using System.Text.RegularExpressions;

namespace LockProvider;

public class LockProvider : IDisposable
{
    public class SemaphoreInfo
    {
        public SemaphoreInfo(string owner, string name, DateTime acquiredAt)
        {
            Owner = owner;
            Name = name;
            AcquiredAt = acquiredAt;
        }

        public string Owner { get; set; }
        public string Name { get; set; }
        public DateTime AcquiredAt { get; set; }
    }

    private class SemaphoreInfoExtended : SemaphoreInfo
    {
        public SemaphoreInfoExtended(string owner, string name, FifoSemaphore semaphore) :
            base(owner, name, DateTime.UtcNow)
        {
            Semaphore = semaphore;
        }

        public string Key => GetKey(Owner, Name);

        public FifoSemaphore Semaphore { get; set; }

        public int CurrentCount => Semaphore.CurrentCount;

        public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return await Semaphore.WaitAsync(timeout, cancellationToken);
        }

        public void Release()
        {
            Semaphore.Release();
        }

        public static string GetKey(string owner, string name)
        {
            return $"{owner}:{name}";
        }
    }

    private readonly FifoSemaphore _mainLock = new(1, 1);
    private readonly Dictionary<string, SemaphoreInfoExtended> _locks = new();

    private readonly FifoSemaphore _waitingLocksLock = new(1, 1);
    private readonly List<string> _waitingLocks = [];

    /// <summary>
    /// Get the count of the current locks
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetLocksCount()
    {
        await _mainLock.WaitAsync();
        var res = _locks.Count;
        _mainLock.Release();
        return res;
    }

    /// <summary>
    /// Get the count of the waiting locks
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetWaitingLocksCount()
    {
        await _waitingLocksLock.WaitAsync();
        var res = _waitingLocks.Count;
        _waitingLocksLock.Release();
        return res;
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
        owner = owner.Trim();
        if (string.IsNullOrEmpty(owner)) {
            throw new ArgumentException("Owner cannot be empty");
        }
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) {
            throw new ArgumentException("Name cannot be empty");
        }

        await _mainLock.WaitAsync();
        var res = _locks.ContainsKey(SemaphoreInfoExtended.GetKey(owner, name));
        _mainLock.Release();
        return res;
    }

    /// <summary>
    /// Acquire a named lock waiting a maximum time
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <param name="timeout">The timeout in seconds</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="TimeoutException"></exception>
    public async Task<bool> AcquireLock(string owner, string name, int timeout)
    {
        owner = owner.Trim();
        if (string.IsNullOrEmpty(owner)) {
            throw new ArgumentException("Owner cannot be empty");
        }
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) {
            throw new ArgumentException("Name cannot be empty");
        }
        if (timeout <= 0) {
            throw new ArgumentException("Timeout must be greater or equal zero");
        }

        var key = SemaphoreInfoExtended.GetKey(owner, name);
        await _waitingLocksLock.WaitAsync();
        _waitingLocks.Add(key);
        _waitingLocksLock.Release();

        SemaphoreInfoExtended? semaphore;
        var createdNew = false;

        await _mainLock.WaitAsync();
        try {
            if (!_locks.TryGetValue(key, out semaphore)) {
                semaphore = new SemaphoreInfoExtended(owner, name, new FifoSemaphore(1, 1));
                _locks[key] = semaphore;
                createdNew = true;
            }
        } finally {
            _mainLock.Release();
        }

        if (!await semaphore.WaitAsync(TimeSpan.FromSeconds(timeout))) {
            if (createdNew) {
                await _mainLock.WaitAsync();
                _locks.Remove(key);
                _mainLock.Release();
            }

            await _waitingLocksLock.WaitAsync();
            _waitingLocks.Remove(key);
            _waitingLocksLock.Release();

            throw new TimeoutException($"Lock '{name}' timed out");
        }

        await _waitingLocksLock.WaitAsync();
        _waitingLocks.Remove(key);
        _waitingLocksLock.Release();

        return true;
    }

    /// <summary>
    /// Release a lock
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<bool> ReleaseLock(string owner, string name)
    {
        owner = owner.Trim();
        if (string.IsNullOrEmpty(owner)) {
            throw new ArgumentException("Owner cannot be empty");
        }
        name = name.Trim();
        if (string.IsNullOrEmpty(name)) {
            throw new ArgumentException("Name cannot be empty");
        }

        await _mainLock.WaitAsync();
        try {
            if (_locks.TryGetValue(SemaphoreInfoExtended.GetKey(owner, name), out var semaphore)) {
                semaphore.Release();
                if (semaphore.CurrentCount == 1) {
                    _locks.Remove(semaphore.Key);
                }
            } else {
                return false;
            }
        } finally {
            _mainLock.Release();
        }

        return true;
    }

    public async Task<List<SemaphoreInfo>> LocksList(string owner, string? nameRegex = null)
    {
        owner = owner.Trim();
        if (string.IsNullOrEmpty(owner)) {
            throw new ArgumentException("Owner cannot be empty");
        }

        var res = new List<SemaphoreInfo>();
        await _mainLock.WaitAsync();
        var locks = _locks.Values.ToList();
        _mainLock.Release();

        Regex? regex = null;
        if (!string.IsNullOrEmpty(nameRegex)) {
            if (!nameRegex.StartsWith("^"))
                nameRegex = $"^{nameRegex}";
            if (!nameRegex.EndsWith("$"))
                nameRegex = $"{nameRegex}$";
            regex = new Regex(nameRegex);
        }

        foreach (var s in locks) {
            if (s.Owner != owner)
                continue;
            if (regex != null && !regex.Match(s.Name).Success)
                continue;

            res.Add(new SemaphoreInfo(s.Owner, s.Name, s.AcquiredAt));
        }

        return res
            .OrderBy(l => l.AcquiredAt)
            .ToList();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) {
            foreach (var kvp in _locks) {
                kvp.Value.Release();
            }
        }
    }
}