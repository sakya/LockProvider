using System.Diagnostics;
using System.Reflection;
using LockProviderApi.Models.Http;
using Microsoft.AspNetCore.Mvc;
using LockResponse = LockProviderApi.Models.Http.LockResponse;
using LocksListResponse = LockProviderApi.Models.Http.LocksListResponse;
using StatusResponse = LockProviderApi.Models.Http.StatusResponse;

namespace LockProviderApi.Http;

[ApiController]
public class LockController : ControllerBase
{
    private static readonly LockProvider.LockProvider LockProvider = Utils.Singleton.GetLockProvider();
    private readonly ILogger<LockController> _logger;

    public LockController(ILogger<LockController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the current status of the server
    /// </summary>
    /// <returns></returns>
    [HttpGet("status")]
    public async Task<ActionResult<StatusResponse>> Status()
    {
        try {
            var res = new StatusResponse()
            {
                Result = true,
                ServerVersion = Assembly.GetExecutingAssembly().GetName().Version,
                Uptime = DateTime.UtcNow - Program.StartedAt,
                Locks = await LockProvider.GetLocksCount(),
                WaitingLocks = await LockProvider.GetWaitingLocksCount(),
            };

            return res;
        } catch (Exception ex) {
            _logger.LogWarning("[Status]Getting status: {ExMessage}", ex.Message);
            return new StatusResponse()
            {
                Result = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Check if a specific lock is acquired.
    /// Owner and name must match exactly.
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <returns></returns>
    [HttpGet("islocked")]
    public async Task<ActionResult<LockResponse>> IsLocked([FromQuery]string owner, [FromQuery]string name)
    {
        try {
            var res = new LockResponse()
            {
                Owner = owner,
                Name = name,
                Result = (await LockProvider.IsLocked(owner, name)),
            };
            return res;
        } catch (Exception ex) {
            _logger.LogWarning("[IsLocked]Error checking lock '{Name}' ({Owner}): {ExMessage}", name, owner, ex.Message);
            return new LockResponse()
            {
                Owner = owner,
                Name = name,
                Result = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// List acquired locks.
    /// The owner must match exactly.
    /// The name is a regex to filter locks.
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name regex</param>
    /// <returns></returns>
    [HttpGet("list")]
    public async Task<ActionResult<LocksListResponse>> List([FromQuery] string owner, [FromQuery] string name)
    {
        try {
            var res = new LocksListResponse()
            {
                Owner = owner,
                Name = name,
                Result = true,
            };
            var locks = await LockProvider.LocksList(owner, name);
            foreach (var l in locks) {
                res.Locks.Add(new LocksListResponse.LockInfo()
                {
                    Owner = l.Owner,
                    Name = l.Name,
                    AcquiredAt = l.AcquiredAt,
                    ExpiresAt = l.ExpiresAt,
                });
            }

            return res;
        } catch (Exception ex) {
            _logger.LogWarning("[List]Getting locks list: {ExMessage}", ex.Message);
            return new LocksListResponse()
            {
                Owner = owner,
                Name = name,
                Result = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Acquire a lock
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("acquire")]
    public async Task<ActionResult<LockResponse>> Acquire([FromBody] AcquireRequest request)
    {
        global::LockProvider.LockProvider.SemaphoreInfo? lockInfo = null;
        try {
            var sw = new Stopwatch();
            sw.Start();
            lockInfo = await LockProvider.AcquireLock(request.Owner, request.Name, request.Timeout, request.TimeToLive);
            sw.Stop();
            _logger.LogDebug("Acquired lock '{RequestName}' ({RequestOwner}), elapsed: {SwElapsed}", request.Name, request.Owner, sw.Elapsed);
        } catch (TimeoutException) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{RequestName}' ({RequestOwner}): Timeout ({RequestTimeout} seconds)", request.Name, request.Owner, request.Timeout);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false,
                Error = "Timeout",
            };
        } catch (Exception ex) {
            _logger.LogWarning("[Acquire]Error acquiring lock '{RequestName}' ({RequestOwner}): {ExMessage}", request.Name, request.Owner, ex.Message);
            return new LockResponse()
            {
                Owner = request.Owner,
                Name = request.Name,
                Result = false,
                Error = ex.Message,
            };
        }

        return new LockResponse()
        {
            Owner = request.Owner,
            Name = request.Name,
            ExpiresAt = lockInfo.ExpiresAt,
            Result = true,

        };
    }

    /// <summary>
    /// Release a lock.
    /// Owner and name must match exactly.
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name</param>
    /// <returns></returns>
    [HttpDelete("release")]
    public async Task<ActionResult<LockResponse>> Release([FromQuery] string owner, [FromQuery] string name)
    {
        try {
            var sw = new Stopwatch();
            sw.Start();
            var res = await LockProvider.ReleaseLock(owner, name);
            sw.Stop();
            if (res) {
                _logger.LogDebug("Released lock '{Name}' ({Owner}), elapsed: {SwElapsed}", name, owner, sw.Elapsed);
                return new LockResponse()
                {
                    Owner = owner,
                    Name = name,
                    Result = true
                };
            }

            _logger.LogWarning("[Release]Error releasing lock '{Name}' ({Owner}): not found", name, owner);
            return new LockResponse()
            {
                Owner = owner,
                Name = name,
                Result = false,
                Error = "NotFound"
            };
        } catch (Exception ex) {
            _logger.LogWarning("[Release]Error releasing lock '{Name}' ({Owner}): {ExMessage}", name, owner, ex.Message);
            return new LockResponse()
            {
                Owner = owner,
                Name = name,
                Result = false,
                Error = ex.Message,
            };
        }
    }

    /// <summary>
    /// Release multiple locks.
    /// The owner must match exactly.
    /// The name is a regex to filter locks.
    /// </summary>
    /// <param name="owner">The lock owner</param>
    /// <param name="name">The lock name regex</param>
    /// <returns></returns>
    [HttpDelete("releasemany")]
    public async Task<ActionResult<LocksListResponse>> ReleaseMany([FromQuery] string owner, [FromQuery] string name)
    {
        try {
            var res = new LocksListResponse()
            {
                Owner = owner,
                Name = name,
                Result = true
            };
            var locks = await LockProvider.LocksList(owner, name);
            foreach (var l in locks) {
                try {
                    if (await LockProvider.ReleaseLock(l.Owner, l.Name)) {
                        res.Locks.Add(new LocksListResponse.LockInfo()
                        {
                            Owner = l.Owner,
                            Name = l.Name,
                            AcquiredAt = l.AcquiredAt
                        });
                    }
                } catch (Exception ex) {
                    _logger.LogWarning("[ReleaseMany]Error releasing lock '{Name}' ({Owner}): {ExMessage}", name, owner, ex.Message);
                }
            }

            return res;
        } catch (Exception ex) {
            return new LocksListResponse()
            {
                Owner = owner,
                Name = name,
                Result = false,
                Error = ex.Message
            };
        }
    }
}