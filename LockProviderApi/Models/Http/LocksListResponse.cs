namespace LockProviderApi.Models.Http;

public class LocksListResponse : LockResponse
{
    public class LockInfo
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
        /// The UTC time the lock was acquired
        /// </summary>
        public DateTime AcquiredAt { get; set; }
    }

    /// <summary>
    /// The list of locks
    /// </summary>
    public List<LockInfo> Locks { get; set; } = [];

    /// <summary>
    /// The number of locks
    /// </summary>
    public int Count => Locks.Count;
}