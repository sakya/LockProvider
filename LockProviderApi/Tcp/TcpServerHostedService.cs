namespace LockProviderApi.Tcp;

public class TcpServerHostedService : BackgroundService
{
    private readonly TcpListener _tcpServer;

    public TcpServerHostedService(TcpListener tcpServer)
    {
        _tcpServer = tcpServer;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _tcpServer.StartAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _tcpServer.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}