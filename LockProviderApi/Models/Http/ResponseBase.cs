namespace LockProviderApi.Models.Http;

public abstract class ResponseBase
{
    public bool Result { get; set; }
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}