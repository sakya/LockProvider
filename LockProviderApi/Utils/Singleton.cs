namespace LockProviderApi.Utils;

public static class Singleton
{
    private static LockProvider.LockProvider? _lockProvider;

    public static void InitLockProvider(LockProvider.LockProvider lockProvider)
    {
        _lockProvider = lockProvider;
    }

    public static LockProvider.LockProvider GetLockProvider()
    {
        return _lockProvider ?? throw new Exception("LockProvider not initialized");
    }
}