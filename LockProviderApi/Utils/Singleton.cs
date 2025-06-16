namespace LockProviderApi.Utils;

public static class Singleton
{
    private static readonly Lock Lock = new();
    private static LockProvider.LockProvider? _lockProvider;

    public static LockProvider.LockProvider GetLockProvider()
    {
        lock (Lock) {
            return _lockProvider ??= new LockProvider.LockProvider();
        }
    }
}