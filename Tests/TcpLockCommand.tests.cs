namespace Tests;

public class TcpLockCommandTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Parse()
    {
        var cmd = LockProviderApi.Tcp.TcpConnectionHandler.TcpCommand.Parse("ACQUIRE;Id=1-e8ba054d-f751-4342-a94a-f613757c18fe;Owner=StressTest;Name=cca27767-5aa3-481a-a982-98225ca8ba4a;Timeout=10;TimeToLive=10;");
        using (Assert.EnterMultipleScope()) {
            Assert.That(cmd.Command, Is.EqualTo("ACQUIRE"));
            Assert.That(cmd.Id, Is.EqualTo("1-e8ba054d-f751-4342-a94a-f613757c18fe"));
            Assert.That(cmd.Owner, Is.EqualTo("StressTest"));
            Assert.That(cmd.Name, Is.EqualTo("cca27767-5aa3-481a-a982-98225ca8ba4a"));
            Assert.That(cmd.Timeout, Is.EqualTo(10));
            Assert.That(cmd.TimeToLive, Is.EqualTo(10));
        }

        cmd = LockProviderApi.Tcp.TcpConnectionHandler.TcpCommand.Parse("ACQUIRE;Id=1-e8ba054d-f751-4342-a94a-f613757c18fe;Owner=Stress\\;Test;Name=cca27767-5aa3-481a-a982-98225ca8ba4a;Timeout=10;TimeToLive=10;");
        using (Assert.EnterMultipleScope()) {
            Assert.That(cmd.Command, Is.EqualTo("ACQUIRE"));
            Assert.That(cmd.Id, Is.EqualTo("1-e8ba054d-f751-4342-a94a-f613757c18fe"));
            Assert.That(cmd.Owner, Is.EqualTo("Stress;Test"));
            Assert.That(cmd.Name, Is.EqualTo("cca27767-5aa3-481a-a982-98225ca8ba4a"));
            Assert.That(cmd.Timeout, Is.EqualTo(10));
            Assert.That(cmd.TimeToLive, Is.EqualTo(10));
        }
    }

    [Test]
    public void ParseErrors()
    {
        var ex = Assert.Throws<Exception>(() => LockProviderApi.Tcp.TcpConnectionHandler.TcpCommand.Parse("ACQUIRE"));
        Assert.That(ex.Message, Is.EqualTo("EmptyCommand"));

        ex = Assert.Throws<Exception>(() => LockProviderApi.Tcp.TcpConnectionHandler.TcpCommand.Parse(";"));
        Assert.That(ex.Message, Is.EqualTo("EmptyCommand"));

        ex = Assert.Throws<Exception>(() => LockProviderApi.Tcp.TcpConnectionHandler.TcpCommand.Parse("ACQUIRE;Test=5"));
        Assert.That(ex.Message, Is.EqualTo("Invalid argument: Test"));
    }
}