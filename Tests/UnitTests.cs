namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task Lock()
    {
        var lp = new LockProvider.LockProvider();

        var res = await lp.AcquireLock("Test", "lock_1", 1);
        Assert.That(res, Is.EqualTo(true));

        var count = await lp.GetLocksCount();
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task LockRelease()
    {
        var lp = new LockProvider.LockProvider();

        var res = await lp.AcquireLock("Test", "lock_1", 1);
        Assert.That(res, Is.EqualTo(true));

        var count = await lp.GetLocksCount();
        Assert.That(count, Is.EqualTo(1));

        res = await lp.ReleaseLock("Test", "lock_1");
        Assert.That(res, Is.EqualTo(true));

        count = await lp.GetLocksCount();
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task LockTimeout()
    {
        var lp = new LockProvider.LockProvider();

        var res = await lp.AcquireLock("Test", "lock_1", 1);
        Assert.That(res, Is.EqualTo(true));

        var ex = Assert.ThrowsAsync<TimeoutException>(async () => await lp.AcquireLock("Test", "lock_1", 1));
        Assert.That(ex, Is.Not.EqualTo(null));
    }

    [Test]
    public async Task ConcurrentAcquireAndRelease()
    {
        var lp = new LockProvider.LockProvider();
        const string owner = "test-owner";
        const string lockName = "resource-lock";
        const int concurrency = 20;
        const int timeoutSeconds = 5;

        var exclusiveCounter = 0;
        var maxExclusiveCount = 0;

        var tasks = new Task[concurrency];

        for (var i = 0; i < concurrency; i++) {
            tasks[i] = Task.Run(async () =>
            {
                var acquired = await lp.AcquireLock(owner, lockName, timeoutSeconds);
                Assert.That(acquired, Is.True, "Failed to acquire lock");

                try {
                    var current = Interlocked.Increment(ref exclusiveCounter);
                    Assert.That(current, Is.EqualTo(1));

                    int prevMax;
                    do {
                        prevMax = maxExclusiveCount;
                        if (current <= prevMax) break;
                    } while (Interlocked.CompareExchange(ref maxExclusiveCount, current, prevMax) !=
                             prevMax);

                    await Task.Delay(50);
                } finally {
                    Interlocked.Decrement(ref exclusiveCounter);
                    var released = await lp.ReleaseLock(owner, lockName);
                    Assert.That(released, Is.True, "Failed to release lock");
                }
            });
        }
        await Task.WhenAll(tasks);

        Assert.That(maxExclusiveCount, Is.EqualTo(1));
    }
}