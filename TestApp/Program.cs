using System.Diagnostics;

namespace TestApp;

class Program
{
    private static readonly List<string> LockNames = new();
    private static readonly LockProvider.LockProvider LockProvider = new();
    private static readonly ManualResetEventSlim StartLine = new(false);

    static async Task Main(string[] args)
    {
        const int numberOfLocks = 1;
        const int numberOfThreads = 100;

        for (var i = 0; i < numberOfLocks; i++) {
            LockNames.Add($"Lock_{i}");
        }

        var tasks = new List<Task>();
        for (var i = 0; i < numberOfThreads; i++) {
            var idx = i;
            tasks.Add(Task.Run(() => GetLock(idx)));
        }

        StartLine.Set();
        await Task.WhenAll(tasks.ToArray());

        if (await LockProvider.GetLocksCount() > 0) {
            Console.WriteLine();
            Console.WriteLine($"Error, locks count: {await LockProvider.GetLocksCount()}");
        }
    }

    private static async Task<bool> GetLock(int id)
    {
        StartLine.Wait();
        var lockName = LockNames[Random.Shared.Next(LockNames.Count)];
        try {
            Console.WriteLine($"[{id}]Acquiring lock '{lockName}'");
            var sw = new Stopwatch();
            sw.Start();
            await LockProvider.AcquireLock("TestApp", lockName, 10);
            sw.Stop();
            Console.WriteLine($"[{id}]Lock '{lockName}' acquired in {sw.Elapsed}, Locks: {await LockProvider.GetLocksCount()}, Waiting: {await LockProvider.GetWaitingLocksCount()}");
            await Task.Delay(500);
            await LockProvider.ReleaseLock("TestApp", lockName);
            Console.WriteLine($"[{id}]Lock '{lockName}' released");
        } catch (Exception ex) {
            Console.WriteLine($"[{id}]{ex.Message}");
            //if (!string.IsNullOrEmpty(ex.StackTrace))
            //    Console.WriteLine(ex.StackTrace);
            return false;
        }

        return true;
    }
}