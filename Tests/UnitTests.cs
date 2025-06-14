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
    public async Task IsLocked()
    {
        var lockProvider = new LockProvider.LockProvider();
        const string owner = "test-owner";
        const string lockName = "resource-lock";
        const int timeoutSeconds = 2;

        var initiallyLocked = await lockProvider.IsLocked(owner, lockName);
        Assert.That(initiallyLocked, Is.False, "Lock should not exist initially");

        var acquired = await lockProvider.AcquireLock(owner, lockName, timeoutSeconds);
        Assert.That(acquired, Is.True, "Failed to acquire lock");

        var lockedAfterAcquire = await lockProvider.IsLocked(owner, lockName);
        Assert.That(lockedAfterAcquire, Is.True, "Lock should exist after acquire");

        var released = await lockProvider.ReleaseLock(owner, lockName);
        Assert.That(released, Is.True, "Failed to release lock");

        var lockedAfterRelease = await lockProvider.IsLocked(owner, lockName);
        Assert.That(lockedAfterRelease, Is.False, "Lock should not exist after release");
    }

    [Test]
    public async Task LocksList_ReturnsCorrectLocksFilteredByOwnerAndNameRegex()
    {
        var lockProvider = new LockProvider.LockProvider();
        const string owner1 = "owner1";
        const string owner2 = "owner2";
        const int timeoutSeconds = 2;

        await lockProvider.AcquireLock(owner1, "lockA", timeoutSeconds);
        await lockProvider.AcquireLock(owner1, "lockB123", timeoutSeconds);
        await lockProvider.AcquireLock(owner1, "specialLock", timeoutSeconds);

        await lockProvider.AcquireLock(owner2, "lockA", timeoutSeconds);
        var allLocksOwner1 = await lockProvider.LocksList(owner1);
        Assert.That(allLocksOwner1.Count, Is.EqualTo(3));
        Assert.That(allLocksOwner1.All(l => l.Owner == owner1));

        var filteredLocks = await lockProvider.LocksList(owner1, "lockB123");
        Assert.That(filteredLocks.Count, Is.EqualTo(1));
        Assert.That(filteredLocks[0].Name, Is.EqualTo("lockB123"));

        var regexLocks = await lockProvider.LocksList(owner1, "lock.*");
        Assert.That(regexLocks.Count, Is.EqualTo(2));
        Assert.That(regexLocks.All(l => l.Name.StartsWith("lock")));

        var locksOwner2 = await lockProvider.LocksList(owner2);
        Assert.That(locksOwner2.Count, Is.EqualTo(1));
        Assert.That(locksOwner2[0].Owner, Is.EqualTo(owner2));
        Assert.That(locksOwner2[0].Name, Is.EqualTo("lockA"));

        foreach (var l in await lockProvider.LocksList(owner1))
            await lockProvider.ReleaseLock(l.Owner, l.Name);
        foreach (var l in await lockProvider.LocksList(owner2))
            await lockProvider.ReleaseLock(l.Owner, l.Name);
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