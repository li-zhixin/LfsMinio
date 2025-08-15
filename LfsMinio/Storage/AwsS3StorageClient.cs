using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using LfsMinio.Configuration;
using Microsoft.Extensions.Logging;

namespace LfsMinio.Storage;

public sealed class AwsS3StorageClient(AppOptions opts, ILogger<AwsS3StorageClient> logger) : IStorageClient
{
    private readonly AppOptions _opts = opts;
    private readonly ILogger<AwsS3StorageClient> _logger = logger;
    private readonly AmazonS3Client _s3 = CreateClient(opts);
    private readonly TransferUtility _transferUtility = new(CreateClient(opts));
    private readonly long _multipartThreshold = 16 * 1024 * 1024; // 16MB

    public async Task UploadAsync(string oid, Stream content, long size, CancellationToken ct)
    {
        // Use TransferUtility for better handling of large files
        if (size > _multipartThreshold)
        {
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = _opts.Bucket!,
                Key = oid,
                InputStream = content,
                ContentType = "application/octet-stream",
                PartSize = 8 * 1024 * 1024 // 8MB parts
            };
            await _transferUtility.UploadAsync(uploadRequest, ct);
        }
        else
        {
            var put = new PutObjectRequest
            {
                BucketName = _opts.Bucket!,
                Key = oid,
                InputStream = content,
                ContentType = "application/octet-stream"
            };
            await _s3.PutObjectAsync(put, ct);
        }
    }

    public async Task DownloadAsync(string oid, Func<Stream, Task> handleStreamAsync, CancellationToken ct)
    {
        var req = new GetObjectRequest
        {
            BucketName = _opts.Bucket!,
            Key = oid
        };
        using var resp = await _s3.GetObjectAsync(req, ct);
        await handleStreamAsync(resp.ResponseStream);
    }

    public async Task ValidateConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // Check bucket connectivity with HeadBucket
            var headBucketRequest = new HeadBucketRequest
            {
                BucketName = _opts.Bucket!
            };
            await _s3.HeadBucketAsync(headBucketRequest, ct);
            _logger.LogDebug("AWS S3 connectivity validated for bucket {Bucket}", _opts.Bucket);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate AWS S3 connectivity for bucket {Bucket}", _opts.Bucket);
            throw new InvalidOperationException($"Cannot connect to AWS S3 bucket '{_opts.Bucket}': {ex.Message}", ex);
        }
    }

    private static AmazonS3Client CreateClient(AppOptions opts)
    {
        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(opts.Region))
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(opts.Region);
        config.Timeout = TimeSpan.FromMinutes(30);
        config.RetryMode = Amazon.Runtime.RequestRetryMode.Adaptive;
        config.MaxErrorRetry = 3;
        return new AmazonS3Client(config);
    }
    
    public void Dispose()
    {
        _transferUtility?.Dispose();
        _s3?.Dispose();
    }
}
