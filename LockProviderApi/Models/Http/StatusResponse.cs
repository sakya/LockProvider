namespace LockProviderApi.Models.Http;

public class StatusResponse : ResponseBase
{
    public Version? ServerVersion { get; set; }
    public TimeSpan Uptime { get; set; }
    public int Locks {  get; set; }
    public int WaitingLocks { get; set; }
}