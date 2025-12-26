namespace LockProviderApi.Models.Http;

public abstract class ResponseBase
{
    /// <summary>
    /// The operation result
    /// </summary>
    public bool Result { get; set; }
    /// <summary>
    /// The current UTC time
    /// </summary>
    public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Optional error message
    /// </summary>
    public string? Error { get; set; }
}