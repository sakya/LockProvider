using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using Grpc.Net.Client;
using LockProviderGrpc;

namespace StressTest;

class Program
{
    public class LockResponse
    {
        public bool Result { get; set; }
        public string? Owner { get; set; }
        public string? Name { get; set; }
        public string? Error { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    private static readonly ManualResetEventSlim StartLine = new(false);
    private static readonly HttpClient HttpClient = new();

    private static void Main(string[] args)
    {
        var mode = SelectMode();
        var threads = SelectThreads();
        var cts = new CancellationTokenSource();

        var tasks = new List<Task>();
        var modeName = string.Empty;
        switch (mode) {
            case "1":
                modeName = "gRPC";
                for (var i = 0; i < threads; i++) {
                    var index = i;
                    tasks.Add(Task.Run(() => StressGrpc(index, cts.Token)));
                }
                break;
            case "2":
                modeName = "REST";
                for (var i = 0; i < threads; i++) {
                    var index = i;
                    tasks.Add(Task.Run(() => StressRest(index, cts.Token)));
                }
                break;
            case "3":
                modeName = "TCP";
                for (var i = 0; i < threads; i++) {
                    var index = i;
                    tasks.Add(Task.Run(() => StressTcp(index, cts.Token)));
                }
                break;
        }

        Console.WriteLine($"Starting {modeName} stress test");
        Console.WriteLine();

        StartLine.Set();
        Task.WaitAll(tasks.ToArray());
    }

    private static string SelectMode()
    {
        while (true) {
            Console.WriteLine("1. gRPC");
            Console.WriteLine("2. REST");
            Console.WriteLine("3. TCP");
            Console.Write("Select mode: ");
            var mode = Console.ReadLine()?.Trim();
            if (mode is "1" or "2" or "3")
                return mode;
        }
    }

    private static int SelectThreads()
    {
        while (true) {
            Console.Write("Input number of threads [1]: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input))
                return 1;

            if (int.TryParse(input, out var threads) && threads > 0) {
                return threads;
            }
        }
    }

    private static async Task<bool> StressGrpc(int index, CancellationToken cancellationToken = default)
    {
        StartLine.Wait(cancellationToken);
        var grpcChannel = GrpcChannel.ForAddress("http://localhost:5000");
        var grpcClient = new LockProvider.LockProviderClient(grpcChannel);

        var count = 0;
        var lastLog = DateTime.UtcNow;

        while (true) {
            if (cancellationToken.IsCancellationRequested) break;

            var lockName = Guid.NewGuid().ToString();
            var acquireReq = new LockAcquireRequest() { Owner = "StressTest", Name = lockName, Timeout = 10, TimeToLive = 10};
            var acquireRes = await grpcClient.AcquireAsync(acquireReq);
            if (acquireRes.Result != "True") {
                Console.WriteLine($"Failed to acquire lock {lockName}");
            }

            var releaseReq = new LockRequest() { Owner = "StressTest", Name = lockName };
            var releaseRes = await grpcClient.ReleaseAsync(releaseReq);
            if (releaseRes.Result != "True") {
                Console.WriteLine($"Failed to acquire lock {lockName}");
            }

            count++;
            var elapsed = (DateTime.UtcNow - lastLog).TotalMilliseconds;
            if (elapsed >= 1000) {
                var perSec = count / elapsed * 1000;
                Console.WriteLine($"[{index}]Locks per second: {Math.Round(perSec):N0}");
                lastLog = DateTime.UtcNow;
                count = 0;
            }
        }

        return true;
    }

    private static async Task<bool> StressRest(int index, CancellationToken cancellationToken = default)
    {
        StartLine.Wait(cancellationToken);

        var count = 0;
        var lastLog = DateTime.UtcNow;
        while (true) {
            if (cancellationToken.IsCancellationRequested) break;

            var lockName = Guid.NewGuid().ToString();
            var content = JsonContent.Create(new
            {
                Owner = "StressTest",
                Name = lockName,
                Timeout = 10,
                TimeToLive = 10
            });
            var acquireRes = await HttpClient.PostAsync("http://localhost:5001/acquire", content, cancellationToken);
            var res = await acquireRes.Content.ReadFromJsonAsync<LockResponse>(cancellationToken);
            if (res?.Result != true) {
                Console.WriteLine($"Failed to acquire lock {lockName}");
            }
            var releaseRes = await HttpClient.DeleteAsync($"http://localhost:5001/release?owner=StressTest&name={lockName}", cancellationToken);
            res = await releaseRes.Content.ReadFromJsonAsync<LockResponse>(cancellationToken);
            if (res?.Result != true) {
                Console.WriteLine($"Failed to release lock {lockName}");
            }

            count++;
            var elapsed = (DateTime.UtcNow - lastLog).TotalMilliseconds;
            if (elapsed >= 1000) {
                var perSec = count / elapsed * 1000;
                Console.WriteLine($"[{index}]Locks per second: {Math.Round(perSec):N0}");
                lastLog = DateTime.UtcNow;
                count = 0;
            }
        }

        return true;
    }

    private static async Task<bool> StressTcp(int index, CancellationToken cancellationToken = default)
    {
        StartLine.Wait(cancellationToken);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        await socket.ConnectAsync("127.0.0.1", 5002, cancellationToken);

        var count = 0;
        var lastLog = DateTime.UtcNow;
        while (true) {
            if (cancellationToken.IsCancellationRequested) break;

            var lockName = Guid.NewGuid().ToString();
            await socket.SendAsync(Encoding.UTF8.GetBytes($"ACQUIRE;Owner=StressTest;Name={lockName};Timeout=10;TimeToLive=10;\n"));

            var buffer = new byte[1024];
            var received = await socket.ReceiveAsync(buffer);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            if (!response.StartsWith("Result=True;")) {
                Console.WriteLine($"Failed to acquire lock {lockName}");
            }

            await socket.SendAsync(Encoding.UTF8.GetBytes($"RELEASE;Owner=StressTest;Name={lockName};\n"));

            buffer = new byte[1024];
            received = await socket.ReceiveAsync(buffer);
            response = Encoding.UTF8.GetString(buffer, 0, received);
            if (!response.StartsWith("Result=True;")) {
                Console.WriteLine($"Failed to release lock {lockName}");
            }

            count++;
            var elapsed = (DateTime.UtcNow - lastLog).TotalMilliseconds;
            if (elapsed >= 1000) {
                var perSec = count / elapsed * 1000;
                Console.WriteLine($"[{index}]Locks per second: {Math.Round(perSec):N0}");
                lastLog = DateTime.UtcNow;
                count = 0;
            }
        }

        return true;
    }
}