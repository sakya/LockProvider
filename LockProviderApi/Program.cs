using System.Reflection;
using LockProviderApi.Grpc;
using LockProviderApi.Tcp;

namespace LockProviderApi;

public class Program
{
    public static DateTime StartedAt;

    public static void Main(string[] args)
    {
        StartedAt = DateTime.UtcNow;
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

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });

        builder.Services.AddSingleton<TcpListener>();
        builder.Services.AddHostedService<TcpServerHostedService>();

        var app = builder.Build();
        var pLogger = app.Services.GetRequiredService<ILogger<Program>>();

        var assembly = Assembly.GetEntryAssembly();
        pLogger.LogInformation("LockProviderApi v{version}", assembly?.GetName().Version?.ToString());
        if (assembly?.GetCustomAttribute(typeof(AssemblyCopyrightAttribute)) is AssemblyCopyrightAttribute ca) {
            pLogger.LogInformation("{copyright}", ca.Copyright);
        }

        var logger = app.Services.GetRequiredService<ILogger<LockProvider.LockProvider>>();
        var lockProvider = Utils.Singleton.GetLockProvider();
        if (lockProvider.Log == null) {
            lockProvider.Log = (level, message) =>
            {
                switch (level) {
                    case LockProvider.LockProvider.LockLogLevel.Debug: logger.LogDebug("{message}", message); break;
                    case LockProvider.LockProvider.LockLogLevel.Info: logger.LogInformation("{message}", message); break;
                    case LockProvider.LockProvider.LockLogLevel.Warning: logger.LogWarning("{message}", message); break;
                    case LockProvider.LockProvider.LockLogLevel.Error: logger.LogError("{message}", message); break;
                    default: logger.LogInformation("{message}", message); break;
                }
            };
        }

        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapGrpcReflectionService();
        app.MapGrpcService<GrpcServer>();
        app.MapControllers();

        app.Run();
    }
}