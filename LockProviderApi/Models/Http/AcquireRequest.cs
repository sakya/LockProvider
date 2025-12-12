namespace LockProviderApi.Models.Http;

public class AcquireRequest
{
    public string Owner { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int Timeout { get; set; }
    public int TimeToLive { get; set; }
}