using System.Net;
using System.Net.Sockets;

namespace LockProviderApi.Tcp;

// https://medium.com/@Alikhalili/building-a-high-performance-tcp-server-from-scratch-a8ede35c4cc2
public class TcpListener
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private Socket? _socket;

    public TcpListener(IConfiguration configuration, ILogger<TcpListener> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var tcpPort = _configuration.GetValue<int>("TcpEndpoint:port");
        if (tcpPort <= 0) {
            _logger.LogWarning("Invalidt TCP port: {TcpPort}", tcpPort);
            return;
        }

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, tcpPort));
        _socket.Listen(4096);

        _logger.LogInformation("Listening for TCP connection on port {TcpPort}", tcpPort);
        while (!ct.IsCancellationRequested) {
            var acceptSocket = await _socket.AcceptAsync(ct);
            _logger.LogInformation("Accepted connection from {AcceptSocketRemoteEndPoint}", acceptSocket.RemoteEndPoint);

            acceptSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            var handler = new TcpConnectionHandler(_logger, acceptSocket);
            _ = Task.Run(() => handler.Execute(), ct);
        }
    }

    public Task StopAsync()
    {
        _socket?.Close();
        _socket?.Dispose();
        return Task.CompletedTask;
    }
}