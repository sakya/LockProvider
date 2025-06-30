using LockProvider;

namespace Tests;

public class FifoSemaphoreTests
{
    [Test]
    public async Task FifoOrder()
    {
        var semaphore = new FifoSemaphore(initialCount: 1);
        var results = new List<int>();
        var tasks = new List<Task>();

        await semaphore.WaitAsync();

        for (var i = 0; i < 5; i++)
        {
            var id = i;
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                lock (results)
                {
                    results.Add(id);
                }
                await Task.Delay(10);
                semaphore.Release();
            });

            tasks.Add(task);
        }

        await Task.Delay(100);

        for (var i = 0; i < 5; i++) {
            semaphore.Release();
            await Task.Delay(20);
        }

        await Task.WhenAll(tasks);

        var expectedOrder = Enumerable.Range(0, 5).ToList();
        Assert.That(results, Is.EqualTo(expectedOrder), "Tasks did not acquire the semaphore in FIFO order.");
    }
}