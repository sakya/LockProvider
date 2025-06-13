﻿namespace LockProvider;

public class FifoSemaphore
{
    private readonly Lock _lock = new();
    private readonly Queue<TaskCompletionSource<bool>> _asyncQueue = new();
    private int _currentCount;
    private readonly int _maxCount;

    public FifoSemaphore(int initialCount, int maxCount = int.MaxValue)
    {
        if (initialCount < 0 || maxCount <= 0 || initialCount > maxCount)
            throw new ArgumentOutOfRangeException();

        _currentCount = initialCount;
        _maxCount = maxCount;
    }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock) {
            if (_currentCount > 0) {
                _currentCount--;
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() =>
                {
                    lock (_lock) {
                        if (_asyncQueue.Contains(tcs)) {
                            _asyncQueue.Enqueue(new TaskCompletionSource<bool>());
                            tcs.TrySetCanceled(cancellationToken);
                        }
                    }
                });
            }

            _asyncQueue.Enqueue(tcs);
            return tcs.Task;
        }
    }

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try {
            await WaitAsync(cts.Token);
            return true;
        } catch (OperationCanceledException) {
            return false;
        }
    }

    public void Release()
    {
        lock (_lock) {
            while (_asyncQueue.Count > 0) {
                var tcs = _asyncQueue.Dequeue();
                if (tcs.Task.IsCompleted) continue;
                tcs.TrySetResult(true);
                return;
            }

            if (_currentCount < _maxCount) {
                _currentCount++;
            } else {
                throw new SemaphoreFullException("Semaphore released too many times.");
            }
        }
    }

    public int CurrentCount
    {
        get
        {
            lock (_lock) {
                return _currentCount;
            }
        }
    }
}