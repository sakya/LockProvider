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
}