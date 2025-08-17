using System.Reactive.Linq;
using LfsMinio.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace LfsMinio.Storage;

public sealed class MinioStorageClient : IStorageClient
{
    private readonly AppOptions _opts;
    private readonly ILogger<MinioStorageClient> _logger;
    private readonly IMinioClient _client;

    public MinioStorageClient(AppOptions opts, ILogger<MinioStorageClient> logger)
    {
        _opts = opts;
        _logger = logger;
        if (string.IsNullOrWhiteSpace(_opts.Endpoint)) throw new ArgumentException("Endpoint required for MinIO/S3-compatible mode");
        if (string.IsNullOrWhiteSpace(_opts.AccessKey) || string.IsNullOrWhiteSpace(_opts.SecretKey)) throw new ArgumentException("Access/Secret required for MinIO/S3-compatible mode");

        _client = new MinioClient()
            .WithEndpoint(_opts.Endpoint)
            .WithCredentials(_opts.AccessKey, _opts.SecretKey)
            .WithSSL(_opts.Secure)
            .Build();
    }

    public async Task UploadAsync(string repo, string oid, Stream content, long size, CancellationToken ct)
    {
        var objectKey = $"{repo}/{oid}";
        var put = new PutObjectArgs()
            .WithBucket(_opts.Bucket!)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType("application/octet-stream");
        await _client.PutObjectAsync(put, ct);
    }

    public async Task DownloadAsync(string repo, string oid, Func<Stream, Task> handleStreamAsync, CancellationToken ct)
    {
        var objectKey = $"{repo}/{oid}";
        var get = new GetObjectArgs()
            .WithBucket(_opts.Bucket!)
            .WithObject(objectKey)
            .WithCallbackStream(s => 
            {
                // Use synchronous bridge to avoid async void in callback
                handleStreamAsync(s).GetAwaiter().GetResult();
            });
        await _client.GetObjectAsync(get, ct);
    }

    public async Task ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // Check bucket connectivity by listing objects with limit
            var listArgs = new ListObjectsArgs()
                .WithBucket(_opts.Bucket!)
;
            await _client.ListObjectsAsync(listArgs, ct);
            _logger.LogDebug("MinIO connectivity validated for bucket {Bucket}", _opts.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate MinIO connectivity for bucket {Bucket}", _opts.Bucket);
            throw new InvalidOperationException($"Cannot connect to MinIO bucket '{_opts.Bucket}': {ex.Message}", ex);
        }
    }
}
