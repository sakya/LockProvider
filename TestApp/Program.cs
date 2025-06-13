using System.Diagnostics;

namespace TestApp;

class Program
{
    private static readonly List<string> LockNames = new();
    private static readonly LockProvider.LockProvider LockProvider = new();

    static void Main(string[] args)
    {
        const int numberOfLocks = 10;
        const int numberOfThreads = 100;

        for (var i = 0; i < numberOfLocks; i++) {
            LockNames.Add($"Lock_{i}");
        }

        var tasks = new List<Task>();
        for (var i = 0; i < numberOfThreads; i++) {
            tasks.Add(GetLock(i));
        }
        Task.WaitAll(tasks.ToArray());
    }

    private static async Task<bool> GetLock(int id)
    {
        var lockName = LockNames[Random.Shared.Next(LockNames.Count)];
        try {
            Console.WriteLine($"[{id}]Acquiring lock '{lockName}'");
            var sw = new Stopwatch();
            sw.Start();
            await LockProvider.AcquireLock("TestApp", lockName, 10);
            sw.Stop();
            Console.WriteLine($"[{id}]Lock '{lockName}' acquired in {sw.Elapsed}, Locks: {await LockProvider.GetLocksCount()}, Waiting: {await LockProvider.GetWaitingLocksCount()}");
            await Task.Delay(Random.Shared.Next(0, 1000));
            Console.WriteLine($"[{id}]Releasing lock '{lockName}'");
            await LockProvider.ReleaseLock("TestApp", lockName);
        } catch (Exception ex) {
            Console.WriteLine($"[{id}]Lock '{lockName}' failed: {ex.Message}");
            //if (!string.IsNullOrEmpty(ex.StackTrace))
            //    Console.WriteLine(ex.StackTrace);
            return false;
        }

        return true;
    }
}