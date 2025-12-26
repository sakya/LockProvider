using System.Diagnostics;
using LockProvider;

namespace Tests;

public class FifoSemaphoreTests
{
    [Test]
    public async Task FifoOrder()
    {
        var semaphore = new FifoSemaphore(initialCount: 1);
        var results = new List<int>();
        var readySignals = new List<TaskCompletionSource<bool>>();

        await semaphore.WaitAsync();

        for (var i = 0; i < 5; i++)
            readySignals.Add(new TaskCompletionSource<bool>());

        var tasks = new List<Task>();

        for (var i = 0; i < 5; i++) {
            var index = i;
            var tcs = readySignals[i];
            var task = Task.Run(async () =>
            {
                await tcs.Task;
                await semaphore.WaitAsync();
                lock (results) {
                    results.Add(index);
                }

                await Task.Delay(10);
                semaphore.Release();
            });
            tasks.Add(task);
        }

        for (var i = 0; i < 5; i++) {
            readySignals[i].SetResult(true);
            await Task.Delay(10);
        }

        semaphore.Release();
        await Task.WhenAll(tasks);

        Assert.That(results, Is.EqualTo(new List<int> { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public async Task Timeout()
    {
        var sem = new FifoSemaphore(0, 1);

        var sw = Stopwatch.StartNew();
        var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(200));
        sw.Stop();

        Assert.That(result, Is.False);
        Assert.That(sw.ElapsedMilliseconds, Is.InRange(150, 1000));
    }

    [Test]
    public async Task WaitAsync_ReturnsTrue_WhenReleasedBeforeTimeout()
    {
        var sem = new FifoSemaphore(0, 1);

        _ = Task.Run(async () =>
        {
            await Task.Delay(100);
            sem.Release();
        });

        var result = await sem.WaitAsync(TimeSpan.FromMilliseconds(500));

        Assert.That(result, Is.True);
    }

    [Test]
    public async Task WaitAsync_ShouldNotHang_WhenOnlyTimeoutIsUsed()
    {
        var sem = new FifoSemaphore(0, 1);

        var tasks = new Task<bool>[50];
        for (int i = 0; i < tasks.Length; i++) {
            tasks[i] = sem.WaitAsync(TimeSpan.FromMilliseconds(100));
        }

        var all = Task.WhenAll(tasks);

        var completed = await Task.WhenAny(all, Task.Delay(2000));
        Assert.That(completed, Is.EqualTo(all), "Some WaitAsync calls never completed");

        var results = await all;
        Assert.That(results, Is.All.False);
    }

    [Test]
    public async Task WaitAsync_ReturnsFalse_WhenExternallyCanceled()
    {
        var sem = new FifoSemaphore(0, 1);
        using var cts = new CancellationTokenSource(100);

        var result = await sem.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        Assert.That(result, Is.False);
    }
}