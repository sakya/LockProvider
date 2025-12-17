using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace LockProviderApi.Tcp;

public sealed class TcpConnectionHandler : IThreadPoolWorkItem, IDisposable
{
    private sealed class LockCommand
    {
        public required string Command { get; init; }
        public string Owner { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Timeout { get; set; }
        public int TimeToLive { get; set; }
    }

    private int _disposed;
    private const int BufferSize = 4096;

    private static readonly LockProvider.LockProvider LockProvider = Utils.Singleton.GetLockProvider();

    private readonly ILogger _logger;
    private readonly Socket _socket;

    private readonly SocketAsyncEventArgs _receiveArgs;
    private readonly byte[] _receiveBuffer;

    private readonly StringBuilder _incoming = new();

    private bool IsClosed => Volatile.Read(ref _disposed) != 0;

    public TcpConnectionHandler(ILogger logger, Socket socket)
    {
        _logger = logger;
        _socket = socket;

        _receiveBuffer = new byte[BufferSize];
        _receiveArgs = new SocketAsyncEventArgs(true);
        _receiveArgs.SetBuffer(_receiveBuffer, 0, _receiveBuffer.Length);
        _receiveArgs.Completed += OnReceiveCompleted;
    }

    public void Execute()
    {
        StartReceive();
    }

    private void StartReceive()
    {
        if (IsClosed) return;

        bool pending;
        try {
            pending = _socket.ReceiveAsync(_receiveArgs);
        } catch (Exception ex) {
            _logger.LogDebug("Receive failed: {Message}", ex.Message);
            Close();
            return;
        }

        if (!pending) {
            OnReceiveCompleted(this, _receiveArgs);
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        if (IsClosed) return;

        if (e.SocketError != SocketError.Success || e.BytesTransferred == 0) {
            _logger.LogInformation("Client disconnected: {Endpoint}", _socket.RemoteEndPoint);
            Close();
            return;
        }

        var text = Encoding.UTF8.GetString(e.Buffer!, 0, e.BytesTransferred);
        _incoming.Append(text);

        ProcessIncomingLines();

        StartReceive();
    }

    /// <summary>
    /// Consumes lines in the buffer
    /// </summary>
    private void ProcessIncomingLines()
    {
        while (true) {
            var newlineIndex = -1;
            for (var i = 0; i < _incoming.Length; i++) {
                if (_incoming[i] == '\n') {
                    newlineIndex = i;
                    break;
                }
            }

            if (newlineIndex < 0)
                break;

            var lineEnd = newlineIndex;
            if (lineEnd > 0 && _incoming[lineEnd - 1] == '\r') {
                lineEnd--;
            }

            var line = _incoming.ToString(0, lineEnd);
            _incoming.Remove(0, newlineIndex + 1);

            if (line.Length == 0)
                continue;

            _ = ProcessCommandAsync(line);
        }
    }

    private async Task ProcessCommandAsync(string line)
    {
        LockCommand cmd;
        try {
            cmd = ParseCommand(line);
        }
        catch (Exception ex) {
            await SendAsync($"Result=False;Error={ex.Message};\n");
            return;
        }

        switch (cmd.Command) {
            case "ACQUIRE":
                await HandleAcquire(cmd);
                break;

            case "RELEASE":
                await HandleRelease(cmd);
                break;

            default:
                _logger.LogWarning("Unknown command '{Command}'", cmd.Command);
                await SendAsync("Result=False;Error=InvalidCommand;\n");
                break;
        }
    }

    private async Task HandleAcquire(LockCommand command)
    {
        try {
            var sw = Stopwatch.StartNew();
            await LockProvider.AcquireLock(command.Owner, command.Name, command.Timeout, command.TimeToLive);
            sw.Stop();

            _logger.LogInformation("Acquired lock '{CommandName}' ({CommandOwner}), elapsed: {SwElapsed}", command.Name, command.Owner, sw.Elapsed);
            await SendAsync($"Result=True;Owner={command.Owner};Name={command.Name};\n");
        }
        catch (TimeoutException) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{CommandName}' ({CommandOwner}): Timeout ({CommandTimeout} seconds)", command.Name, command.Owner, command.Timeout);
            await SendAsync($"Result=False;Owner={command.Owner};Name={command.Name};Error=Timeout;\n");
        }
        catch (Exception ex) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{CommandName}' ({CommandOwner}): {ExMessage}", command.Name, command.Owner, ex.Message);
            await SendAsync($"Result=False;Owner={command.Owner};Name={command.Name};Error={ex.Message};\n");
        }
    }

    private async Task HandleRelease(LockCommand command)
    {
        try {
            var sw = Stopwatch.StartNew();
            var released = await LockProvider.ReleaseLock(command.Owner, command.Name);
            sw.Stop();

            if (released) {
                _logger.LogInformation("Released lock '{CommandName}' ({CommandOwner}), elapsed: {SwElapsed}", command.Name, command.Owner, sw.Elapsed);
                await SendAsync($"Result=True;Owner={command.Owner};Name={command.Name};\n");
            } else {
                _logger.LogWarning("[Release]Error releasing lock '{CommandName}' ({CommandOwner}): not found", command.Name, command.Owner);
                await SendAsync($"Result=False;Owner={command.Owner};Name={command.Name};Error=NotFound;\n");
            }
        } catch (Exception ex) {
            _logger.LogWarning("[Release]Error releasing lock '{CommandName}' ({CommandOwner}): {ExMessage}", command.Name, command.Owner, ex.Message);
            await SendAsync($"Result=False;Owner={command.Owner};Name={command.Name};Error={ex.Message};\n");
        }
    }

    private async Task SendAsync(string text)
    {
        if (IsClosed) return;

        try {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _socket.SendAsync(bytes, SocketFlags.None);
        } catch {
            Close();
        }
    }

    private void Close()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try { _socket.Shutdown(SocketShutdown.Both); } catch {
            // ignored
        }

        try { _socket.Close(); } catch {
            // ignored
        }

        try { _socket.Dispose(); } catch {
            // ignored
        }

        _receiveArgs.Completed -= OnReceiveCompleted;
        _receiveArgs.Dispose();
    }

    public void Dispose()
    {
        Close();
    }

    private static LockCommand ParseCommand(string command)
    {
        var parts = command.Split(';', StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new Exception("EmptyCommand");

        var cmd = new LockCommand { Command = parts[0].ToUpperInvariant() };
        if (string.IsNullOrEmpty(cmd.Command))
            throw new Exception("EmptyCommand");

        foreach (var part in parts.Skip(1)) {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) continue;

            switch (kv[0]) {
                case "Owner": cmd.Owner = kv[1]; break;
                case "Name": cmd.Name = kv[1]; break;
                case "Timeout": cmd.Timeout = int.Parse(kv[1]); break;
                case "TimeToLive": cmd.TimeToLive = int.Parse(kv[1]); break;
            }
        }

        return cmd;
    }
}
