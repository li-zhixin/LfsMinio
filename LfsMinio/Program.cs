using System.Diagnostics;
using LfsMinio.Configuration;
using LfsMinio.Infra;
using LfsMinio.Protocol;
using LfsMinio.Storage;
using LfsMinio.Transfers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace LfsMinio;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Configure Serilog
        var executableDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName) ?? AppContext.BaseDirectory;
        var logPath = Path.Combine(executableDir, "logs", "lfsminio-.log");
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Use Serilog for logging
            builder.Services.AddSerilog();

        // Options & configuration
        builder.Services.AddOptions<AppOptions>().Configure(AppOptions.BindFromEnvironment);

        // Infrastructure
        builder.Services.AddSingleton<IResponder, LfsResponder>();
        builder.Services.AddSingleton<ILfsReader, LfsReader>();
        builder.Services.AddSingleton<IRetryPolicy, ExponentialBackoffRetryPolicy>();

        // Storage selection (deferred factory based on env)
        builder.Services.AddSingleton<IStorageClient>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("StorageSelect");
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>().Value;

            if (!string.IsNullOrWhiteSpace(opts.Endpoint))
            {
                LogUsingS3CompatibleEndpointEndpoint(logger, opts.Endpoint);
                return new MinioStorageClient(opts, sp.GetRequiredService<ILogger<MinioStorageClient>>());
            }

            LogUsingAwsS3RegionRegion(logger, opts.Region ?? "<default>");
            return new AwsS3StorageClient(opts, sp.GetRequiredService<ILogger<AwsS3StorageClient>>());
        });

        // Transfers
        builder.Services.AddSingleton<ITransferService, TransferService>();

        using var app = builder.Build();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var responder = app.Services.GetRequiredService<IResponder>();
        var reader = app.Services.GetRequiredService<ILfsReader>();
        var transfers = app.Services.GetRequiredService<ITransferService>();
        var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AppOptions>>().Value;

        // Set up process exit handlers for cleanup
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => {
            e.Cancel = true;
            LogReceivedSigintShuttingDown(logger);
            cts.Cancel();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => {
            LogProcessExitCleanup(logger);
            cts.Cancel();
        };

        // Use a single stdin stream instance throughout
        var stdin = Console.OpenStandardInput();
        
        // Read first event: init
        await foreach (var evt in reader.ReadEventsAsync(stdin, cts.Token))
        {
            if (evt is InitEvent init)
            {
                // Validate required env/config for independent mode
                try
                {
                    ValidateConfig(options);
                }
                catch (Exception ex)
                {
                    await responder.WriteAsync(new InitErrorResponse(ex.Message), cts.Token);
                    return 1;
                }

                // Ack init
                await responder.WriteAsync(new InitOkResponse(), cts.Token);

                // Force concurrency to 1 when init.concurrent is false
                var concurrency = !init.Concurrent ? 1 : Math.Max(1, init.ConcurrentTransfers > 0 ? init.ConcurrentTransfers : 3);
                LogStartingTransferLoopWithConcurrencyConcurrency(logger, concurrency);

                var result = await RunLoopAsync(reader, transfers, concurrency, stdin, cts.Token);
                return result;
            }
            else
            {
                // Ignore any non-init until init is seen
                LogReceivedNonInitEventBeforeInitIgnoring(logger);
            }
        }

        LogNoInitEventReceivedExiting(logger);
        return 1;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void ValidateConfig(AppOptions opts)
    {
        // Independent mode requires either MinIO envs or AWS envs (rely on default chain for AWS)
        var minioOk = !string.IsNullOrWhiteSpace(opts.Bucket) && !string.IsNullOrWhiteSpace(opts.Endpoint)
                       && !string.IsNullOrWhiteSpace(opts.AccessKey) && !string.IsNullOrWhiteSpace(opts.SecretKey);
        var awsOk = !string.IsNullOrWhiteSpace(opts.Bucket);

        if (!minioOk && !awsOk)
        {
            throw new InvalidOperationException("Missing storage configuration: require LFS_S3_BUCKET and either LFS_S3_ENDPOINT(+ACCESS/SECRET) for S3-compatible, or AWS default creds for AWS.");
        }
    }

    private static async Task<int> RunLoopAsync(ILfsReader reader,  ITransferService transfers, int concurrency, Stream stdin, CancellationToken token)
    {
        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new List<Task>();

        await foreach (var evt in reader.ReadEventsAsync(stdin, token))
        {
            switch (evt)
            {
                case UploadEvent up:
                    await sem.WaitAsync(token);
                    tasks.Add(Task.Run(async () =>
                    {
                        try { await transfers.UploadAsync(up, token); }
                        finally { sem.Release(); }
                    }, token));
                    break;
                case DownloadEvent down:
                    await sem.WaitAsync(token);
                    tasks.Add(Task.Run(async () =>
                    {
                        try { await transfers.DownloadAsync(down, token); }
                        finally { sem.Release(); }
                    }, token));
                    break;
                case TerminateEvent _:
                    await Task.WhenAll(tasks);
                    await transfers.CleanupAsync(token);
                    return 0;
            }
        }

        await Task.WhenAll(tasks);
        await transfers.CleanupAsync(token);
        return 0;
    }

    [LoggerMessage(LogLevel.Information, "Using S3-compatible endpoint {endpoint}")]
    static partial void LogUsingS3CompatibleEndpointEndpoint(ILogger logger, string endpoint);

    [LoggerMessage(LogLevel.Information, "Using AWS S3 region {region}")]
    static partial void LogUsingAwsS3RegionRegion(ILogger logger, string region);

    [LoggerMessage(LogLevel.Warning, "Received SIGINT, shutting down...")]
    static partial void LogReceivedSigintShuttingDown(ILogger<Program> logger);

    [LoggerMessage(LogLevel.Warning, "Process exit, cleanup...")]
    static partial void LogProcessExitCleanup(ILogger<Program> logger);

    [LoggerMessage(LogLevel.Information, "Starting transfer loop with concurrency {concurrency}")]
    static partial void LogStartingTransferLoopWithConcurrencyConcurrency(ILogger<Program> logger, int concurrency);

    [LoggerMessage(LogLevel.Warning, "Received non-init event before init; ignoring.")]
    static partial void LogReceivedNonInitEventBeforeInitIgnoring(ILogger<Program> logger);

    [LoggerMessage(LogLevel.Error, "No init event received; exiting.")]
    static partial void LogNoInitEventReceivedExiting(ILogger<Program> logger);
}
