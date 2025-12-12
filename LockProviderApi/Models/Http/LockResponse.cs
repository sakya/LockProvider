namespace LockProviderApi.Models.Http;

public class LockResponse : ResponseBase
{
    public string Owner { get; set; } = null!;
    public string Name { get; set; } = null!;
}