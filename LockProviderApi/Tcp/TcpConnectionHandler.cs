using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace LockProviderApi.Tcp;

public sealed partial class TcpConnectionHandler : IThreadPoolWorkItem, IDisposable
{
    public class LockCommand
    {
        public required string Command { get; init; }
        public string? Id { get; set; }
        public string Owner { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Timeout { get; set; }
        public int TimeToLive { get; set; }

        public static LockCommand Parse(string command)
        {
            var idx = command.IndexOf(';');
            if (idx <= 0) {
                throw new Exception("EmptyCommand");
            }

            var commandString = command[..idx].Trim().ToUpperInvariant();
            var valueString = command[(idx + 1)..].Trim();

            var cmd = new LockCommand { Command = commandString };
            var regex = NameValueRegex();
            var matches = regex.Matches(valueString);
            foreach (Match match in matches) {
                var name = match.Groups["name"].Value.Trim();
                var value = match.Groups["value"].Value.Trim();
                switch (name) {
                    case "Id":
                        cmd.Id = value.Replace("\\;", ";");
                        break;
                    case "Owner":
                        cmd.Owner = value.Replace("\\;", ";");
                        break;
                    case "Name":
                        cmd.Name = value.Replace("\\;", ";");
                        break;
                    case "Timeout":
                        cmd.Timeout = int.Parse(value);
                        break;
                    case "TimeToLive":
                        cmd.TimeToLive = int.Parse(value);
                        break;
                    default:
                        throw new Exception($"Invalid argument: {name}");
                }
            }

            return cmd;
        }
    }


    [GeneratedRegex(@"(?:^|;)(?<name>[^=;]+)=(?<value>(?:\\;|[^;])*)")]
    private static partial Regex NameValueRegex();

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

    /// <summary>
    /// Process a command (a line of text)
    /// </summary>
    /// <param name="command">The command text</param>
    /// <exception cref="Exception"></exception>
    private async Task ProcessCommandAsync(string command)
    {
        LockCommand cmd;
        try {
            cmd = LockCommand.Parse(command);
            if (string.IsNullOrEmpty(cmd.Id))
                throw new Exception("Missing command id");
        } catch (Exception ex) {
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Error", ex.Message}
            });
            return;
        }

        switch (cmd.Command) {
            case "ISLOCKED":
                await HandleIsLocked(cmd);
                break;

            case "ACQUIRE":
                await HandleAcquire(cmd);
                break;

            case "RELEASE":
                await HandleRelease(cmd);
                break;

            case "RELEASEMANY":
                await HandleReleaseMany(cmd);
                break;

            case "STATUS":
                await HandleStatus(cmd);
                break;

            default:
                _logger.LogWarning("Unknown command '{Command}'", cmd.Command);
                await SendAsync(new Dictionary<string, string?>()
                {
                    { "Result", "False" },
                    { "Id", cmd.Id },
                    { "Error", "InvalidCommand"}
                });
                break;
        }
    }

    private async Task HandleAcquire(LockCommand command)
    {
        try {
            var sw = Stopwatch.StartNew();
            await LockProvider.AcquireLock(command.Owner, command.Name, command.Timeout, command.TimeToLive);
            sw.Stop();

            _logger.LogDebug("Acquired lock '{CommandName}' ({CommandOwner}), elapsed: {SwElapsed}", command.Name, command.Owner, sw.Elapsed);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "True" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
            });
        }
        catch (TimeoutException) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{CommandName}' ({CommandOwner}): Timeout ({CommandTimeout} seconds)", command.Name, command.Owner, command.Timeout);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
                { "Error", "Timeout"}
            });
        }
        catch (Exception ex) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{CommandName}' ({CommandOwner}): {ExMessage}", command.Name, command.Owner, ex.Message);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
                { "Error", ex.Message}
            });
        }
    }

    private async Task HandleRelease(LockCommand command)
    {
        try {
            var sw = Stopwatch.StartNew();
            var released = await LockProvider.ReleaseLock(command.Owner, command.Name);
            sw.Stop();

            if (released) {
                _logger.LogDebug("Released lock '{CommandName}' ({CommandOwner}), elapsed: {SwElapsed}", command.Name, command.Owner, sw.Elapsed);
                await SendAsync(new Dictionary<string, string?>()
                {
                    { "Result", "True" },
                    { "Id", command.Id },
                    { "Owner", command.Owner },
                    { "Name", command.Name },
                });
            } else {
                _logger.LogWarning("[Release]Error releasing lock '{CommandName}' ({CommandOwner}): not found", command.Name, command.Owner);
                await SendAsync(new Dictionary<string, string?>()
                {
                    { "Result", "False" },
                    { "Id", command.Id },
                    { "Owner", command.Owner },
                    { "Name", command.Name },
                    { "Error", "NotFound"}
                });
            }
        } catch (Exception ex) {
            _logger.LogWarning("[Release]Error releasing lock '{CommandName}' ({CommandOwner}): {ExMessage}", command.Name, command.Owner, ex.Message);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
                { "Error", ex.Message}
            });
        }
    }

    private async Task HandleReleaseMany(LockCommand command)
    {
        try {
            var count = 0;
            var locks = await LockProvider.LocksList(command.Owner, command.Name);
            foreach (var l in locks) {
                try {
                    if (await LockProvider.ReleaseLock(l.Owner, l.Name)) {
                        count++;
                    }
                } catch (Exception ex) {
                    _logger.LogWarning("[ReleaseMany]Error releasing lock '{Name}' ({Owner}): {ExMessage}", command.Name, command.Owner, ex.Message);
                }
            }

            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "True" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
                { "Count", count.ToString(CultureInfo.InvariantCulture) },
            });
        } catch (Exception ex) {
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Owner", command.Owner },
                { "Name", command.Name },
                { "Error", ex.Message}
            });
        }
    }

    private async Task HandleStatus(LockCommand command)
    {
        try {
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "True" },
                { "Id", command.Id },
                { "ServerVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString() },
                { "Uptime", (DateTime.UtcNow - Program.StartedAt).ToString("g") },
                { "Locks", (await LockProvider.GetLocksCount()).ToString(CultureInfo.InvariantCulture) },
                { "WaitingLocks", (await LockProvider.GetWaitingLocksCount()).ToString(CultureInfo.InvariantCulture) }
            });
        } catch (Exception ex) {
            _logger.LogWarning("[Status]Error getting status: {ExMessage}", ex.Message);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Error", ex.Message}
            });
        }
    }

    private async Task HandleIsLocked(LockCommand command)
    {
        try {
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", (await LockProvider.IsLocked(command.Owner, command.Name)).ToString() },
                { "Id", command.Id },
            });
        } catch (Exception ex) {
            _logger.LogWarning("[IsLocked]Error checking lock '{Name}' ({Owner}): {ExMessage}", command.Name, command.Owner, ex.Message);
            await SendAsync(new Dictionary<string, string?>()
            {
                { "Result", "False" },
                { "Id", command.Id },
                { "Error", ex.Message}
            });
        }
    }

    private Task SendAsync(Dictionary<string, string?> values)
    {
        if (!values.ContainsKey("TimeStamp")) {
            values["TimeStamp"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        var sb = new StringBuilder();
        foreach (var kvp in values.OrderBy(k => k.Key)) {
            sb.Append($"{kvp.Key}={kvp.Value};");
        }
        sb.Append('\n');

        return SendAsync(sb.ToString());
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
}
