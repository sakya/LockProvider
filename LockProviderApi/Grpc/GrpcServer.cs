using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Grpc.Core;
using LockProviderGrpc;

namespace LockProviderApi.Grpc;

public class GrpcServer : LockProviderGrpc.LockProvider.LockProviderBase
{
    private static readonly LockProvider.LockProvider LockProvider = Utils.Singleton.GetLockProvider();

    private readonly ILogger<GrpcServer> _logger;

    public GrpcServer(ILogger<GrpcServer> logger)
    {
        _logger = logger;
    }

    public override async Task<LockResponse> Acquire(LockAcquireRequest request, ServerCallContext context)
    {
        try {
            var sw = new Stopwatch();
            sw.Start();
            await LockProvider.AcquireLock(request.Owner, request.Name, request.Timeout);
            sw.Stop();
            _logger.LogInformation($"Acquired lock '{request.Name}', elapsed: {sw.Elapsed}");
        } catch (TimeoutException) {
            return new LockResponse()
            {
                Name = request.Name,
                Result = false.ToString(),
                Error = "Timeout",
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        } catch (Exception ex) {
            return new LockResponse()
            {
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }

        return new LockResponse()
        {
            Name = request.Name,
            Result = true.ToString(),
            TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<LockResponse> IsLocked(LockRequest request, ServerCallContext context)
    {
        return new LockResponse()
        {
            Name = request.Name,
            Result = (await LockProvider.IsLocked(request.Name)).ToString(),
            TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<LockResponse> Release(LockRequest request, ServerCallContext context)
    {
        try {
            var sw = new Stopwatch();
            sw.Start();
            var res = await LockProvider.ReleaseLock(request.Name);
            sw.Stop();
            if (res) {
                _logger.LogInformation($"Released lock '{request.Name}', elapsed: {sw.Elapsed}");
            } else {
                _logger.LogWarning($"Lock '{request.Name}' not found");
            }
        } catch (Exception ex) {
            return new LockResponse()
            {
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }

        return new LockResponse()
        {
            Name = request.Name,
            Result = true.ToString(),
            TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<StatusResponse> Status(empty request, ServerCallContext context)
    {
        return new StatusResponse()
        {
            ServerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            Uptime = $"{DateTime.UtcNow - Program.StartedAt}",
            Locks = await LockProvider.GetLocksCount(),
            WaitingLocks = await LockProvider.GetWaitingLocksCount(),
            TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<LocksListResponse> LocksList(empty request, ServerCallContext context)
    {
        var res = new LocksListResponse();
        var locks = await LockProvider.LocksList();
        foreach (var l in locks) {
            res.Locks.Add(new LockInfo()
            {
                Owner = l.Owner,
                Name = l.Name,
                AcquiredAt = l.AcquiredAt.ToString("o", CultureInfo.InvariantCulture)
            });
        }
        return res;
    }
}