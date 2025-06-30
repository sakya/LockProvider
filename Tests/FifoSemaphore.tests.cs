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
}