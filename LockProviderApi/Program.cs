using LockProviderApi.Grpc;

namespace LockProviderApi;

public class Program
{
    public static readonly DateTime StartedAt = DateTime.UtcNow;

    public static void Main(string[] args)
    {
        ThreadPool.GetMaxThreads(out _, out var maxIo);
        ThreadPool.SetMinThreads(workerThreads: 2000, completionPortThreads: maxIo);
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.Http2.MaxStreamsPerConnection = 1000;
            options.Limits.MaxConcurrentConnections = null;
            options.Limits.MaxRequestBodySize = 1 * 1024 * 1024;
            options.Limits.Http2.InitialConnectionWindowSize = 2 * 1024 * 1024;
            options.Limits.Http2.InitialStreamWindowSize = 4 * 1024 * 1024;
        });

        builder.Services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = false;
            options.MaxReceiveMessageSize = 1 * 1024 * 1024;
            options.MaxSendMessageSize = 4 * 1024 * 1024;
        });
        builder.Services.AddGrpcReflection();

        var app = builder.Build();

        app.MapGrpcReflectionService();
        app.MapGrpcService<GrpcServer>();

        app.Run();
    }
}