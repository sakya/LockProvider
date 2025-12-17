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
            await LockProvider.AcquireLock(request.Owner, request.Name, request.Timeout, request.TimeToLive);
            sw.Stop();
            _logger.LogInformation("Acquired lock '{RequestName}', elapsed: {SwElapsed}", request.Name, sw.Elapsed);
        } catch (TimeoutException) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{RequestName}' ({RequestOwner}): Timeout ({RequestTimeout} seconds)", request.Name, request.Owner, request.Timeout);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = "Timeout",
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        } catch (Exception ex) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{RequestName}' ({RequestOwner}): {ExMessage}", request.Name, request.Owner, ex.Message);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }

        return new LockResponse()
        {
            Owner = request.Owner,
            Name = request.Name,
            Result = true.ToString(),
            TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };
    }

    public override async Task<LockResponse> IsLocked(LockRequest request, ServerCallContext context)
    {
        try {
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = (await LockProvider.IsLocked(request.Owner, request.Name)).ToString(),
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        } catch (Exception ex) {
            _logger.LogWarning("[IsLocked]Error checking lock '{RequestName}' ({RequestOwner}): {ExMessage}", request.Name, request.Owner, ex.Message);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }
    }

    public override async Task<LockResponse> Release(LockRequest request, ServerCallContext context)
    {
        try {
            var sw = new Stopwatch();
            sw.Start();
            var res = await LockProvider.ReleaseLock(request.Owner, request.Name);
            sw.Stop();
            if (res) {
                _logger.LogInformation("Released lock '{RequestName}', elapsed: {SwElapsed}", request.Name, sw.Elapsed);
                return new LockResponse()
                {
                    Owner = request.Owner,
                    Name = request.Name,
                    Result = true.ToString(),
                    TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                };
            }

            _logger.LogWarning("[Release]Error releasing lock '{RequestName}' ({RequestOwner}): not found", request.Name, request.Owner);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Error = "NotFound"
            };
        } catch (Exception ex) {
            _logger.LogWarning("[Release]Error releasing lock '{RequestName}' ({RequestOwner}): {ExMessage}", request.Name, request.Owner, ex.Message);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }
    }

    public override async Task<LocksListResponse> ReleaseMany(LockRequest request, ServerCallContext context)
    {
        try {
            var res = new LocksListResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Result = true.ToString()
            };
            var locks = await LockProvider.LocksList(request.Owner, request.Name);
            foreach (var l in locks) {
                try {
                    if (await LockProvider.ReleaseLock(l.Owner, l.Name)) {
                        res.Locks.Add(new LockInfo()
                        {
                            Owner = l.Owner,
                            Name = l.Name,
                            AcquiredAt = l.AcquiredAt.ToString("o", CultureInfo.InvariantCulture)
                        });
                    }
                } catch (Exception ex) {
                    _logger.LogWarning("[ReleaseMany]Error releasing lock '{RequestName}' ({RequestOwner}): {ExMessage}", request.Name, request.Owner, ex.Message);
                }
            }

            res.Count = res.Locks.Count;
            return res;
        } catch (Exception ex) {
            return new LocksListResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }
    }

    public override async Task<StatusResponse> Status(empty request, ServerCallContext context)
    {
        try {
            return new StatusResponse()
            {
                Result = true.ToString(),
                ServerVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                Uptime = $"{DateTime.UtcNow - Program.StartedAt}",
                Locks = await LockProvider.GetLocksCount(),
                WaitingLocks = await LockProvider.GetWaitingLocksCount(),
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        } catch (Exception ex) {
            _logger.LogWarning($"[Status]Getting status: {ex.Message}");
            return new StatusResponse()
            {
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }
    }

    public override async Task<LocksListResponse> List(LocksListRequest request, ServerCallContext context)
    {
        try {
            var res = new LocksListResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                Result = true.ToString(),
            };
            var locks = await LockProvider.LocksList(request.Owner, request.Name);
            foreach (var l in locks) {
                res.Locks.Add(new LockInfo()
                {
                    Owner = l.Owner,
                    Name = l.Name,
                    AcquiredAt = l.AcquiredAt.ToString("o", CultureInfo.InvariantCulture)
                });
            }

            res.Count = res.Locks.Count;
            return res;
        } catch (Exception ex) {
            _logger.LogWarning($"[List]Getting locks list: {ex.Message}");
            return new LocksListResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false.ToString(),
                Error = ex.Message,
                TimeStamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };
        }
    }
}