namespace LockProviderApi.Models.Http;

public class ReleaseRequest
{
    public string Owner { get; set; } = null!;
    public string Name { get; set; } = null!;
}