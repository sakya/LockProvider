namespace LockProviderApi.Models.Http;

public class AcquireRequest
{
    /// <summary>
    /// The lock owner
    /// </summary>
    public string Owner { get; set; } = null!;
    /// <summary>
    /// The lock name
    /// </summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// The maximum number of seconds to wait for the lock
    /// </summary>
    public int Timeout { get; set; }
    /// <summary>
    /// The lock time to live
    /// </summary>
    public int TimeToLive { get; set; }
}