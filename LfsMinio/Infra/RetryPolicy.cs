using System.Net.Sockets;

namespace LfsMinio.Infra;

public interface IRetryPolicy
{
    Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct);
}

public sealed class ExponentialBackoffRetryPolicy(Microsoft.Extensions.Options.IOptions<Configuration.AppOptions> opts)
    : IRetryPolicy
{
    private readonly Configuration.AppOptions _opts = opts.Value;
    private readonly Random _rng = new();

    public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var attempt = 0;
        Exception? last = null;
        var max = Math.Max(1, _opts.RetryMaxAttempts);
        while (attempt < max && !ct.IsCancellationRequested)
        {
            try
            {
                await action(ct);
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < max - 1)
            {
                last = ex;
                attempt++;
                var delay = ComputeDelay(attempt);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        throw last ?? new Exception("Operation failed with no exception");
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var baseDelay = _opts.RetryBaseDelay.TotalMilliseconds;
        var maxDelay = _opts.RetryMaxDelay.TotalMilliseconds;
        var exp = Math.Min(maxDelay, baseDelay * Math.Pow(2, attempt - 1));
        var jitter = _rng.NextDouble() * 0.2 + 0.9; // 0.9x - 1.1x
        return TimeSpan.FromMilliseconds(Math.Min(maxDelay, exp * jitter));
    }

    private static bool IsTransient(Exception ex)
    {
        return ex switch
        {
            // Network/IO errors
            IOException _ => true,
            TaskCanceledException _ => true,
            OperationCanceledException _ => true,
            HttpRequestException _ => true,
            SocketException _ => true,
            
            // AWS S3 specific errors
            Amazon.S3.AmazonS3Exception s3Ex => IsTransientS3Exception(s3Ex),
            
            // MinIO/HTTP generic errors
            Minio.Exceptions.MinioException minioEx => IsTransientMinioException(minioEx),
            
            // Generic timeout/connection errors
            TimeoutException _ => true,
            
            _ => false
        };
    }
    
    private static bool IsTransientS3Exception(Amazon.S3.AmazonS3Exception ex)
    {
        return ex.StatusCode switch
        {
            System.Net.HttpStatusCode.InternalServerError => true,
            System.Net.HttpStatusCode.BadGateway => true,
            System.Net.HttpStatusCode.ServiceUnavailable => true,
            System.Net.HttpStatusCode.GatewayTimeout => true,
            System.Net.HttpStatusCode.TooManyRequests => true,
            System.Net.HttpStatusCode.RequestTimeout => true,
            _ => ex.ErrorCode switch
            {
                "RequestTimeout" => true,
                "SlowDown" => true,
                "Throttling" => true,
                "InternalError" => true,
                "ServiceUnavailable" => true,
                _ => false
            }
        };
    }
    
    private static bool IsTransientMinioException(Minio.Exceptions.MinioException ex)
    {
        // Check for network/connection related MinIO errors
        var message = ex.Message?.ToLowerInvariant() ?? "";
        return message.Contains("timeout") || 
               message.Contains("connection") || 
               message.Contains("network") ||
               message.Contains("reset") ||
               ex.InnerException is IOException or SocketException or HttpRequestException;
    }
}

