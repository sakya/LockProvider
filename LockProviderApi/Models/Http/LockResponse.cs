namespace LockProviderApi.Models.Http;

public class LockResponse : ResponseBase
{
    /// <summary>
    /// The lock owner
    /// </summary>
    public string Owner { get; set; } = null!;
    /// <summary>
    /// The lock name
    /// </summary>
    public string Name { get; set; } = null!;
}