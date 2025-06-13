namespace LockProviderApi.Utils;

public static class Singleton
{
    private static readonly Lock _lock = new();
    private static LockProvider.LockProvider? _lockProvider;

    public static LockProvider.LockProvider GetLockProvider()
    {
        lock (_lock) {
            return _lockProvider ??= new LockProvider.LockProvider();
        }
    }
}