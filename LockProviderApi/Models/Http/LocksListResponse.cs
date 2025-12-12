namespace LockProviderApi.Models.Http;

public class LocksListResponse : LockResponse
{
    public class LockInfo
    {
        public string Owner { get; set; } = null!;
        public string Name { get; set; } = null!;
        public DateTime AcquiredAt { get; set; }
    }

    public List<LockInfo> Locks { get; set; } = [];

    public int Count => Locks.Count;
}