namespace LfsMinio.Configuration;

public sealed class AppOptions
{
    // Unified S3-style options
    public string? Bucket { get; set; }
    public string? Endpoint { get; set; } // when set => use MinIO/S3-compatible endpoint; else AWS
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool Secure { get; set; } = true;
    public string? Region { get; set; } // used by AWS


    // Retry
    public int RetryMaxAttempts { get; set; } = 4;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(300);
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    public static void BindFromEnvironment(AppOptions o)
    {
        // Preferred unified names
        o.Bucket = Environment.GetEnvironmentVariable("LFS_S3_BUCKET");
        o.Endpoint = Environment.GetEnvironmentVariable("LFS_S3_ENDPOINT");
        o.AccessKey =
            Environment.GetEnvironmentVariable("LFS_S3_ACCESS_KEY");


        o.SecretKey =
            Environment.GetEnvironmentVariable("LFS_S3_SECRET_KEY");

        var secureVar = Environment.GetEnvironmentVariable("LFS_S3_SECURE");
        o.Secure = !string.IsNullOrEmpty(secureVar) && secureVar != "0" && secureVar != "false";
        o.Region = Environment.GetEnvironmentVariable("AWS_REGION");


        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_MAX_ATTEMPTS"), out var r) && r > 0)
            o.RetryMaxAttempts = r;

        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_BASE_MS"), out var b) && b > 0)
            o.RetryBaseDelay = TimeSpan.FromMilliseconds(b);

        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_MAX_MS"), out var m) && m > 0)
            o.RetryMaxDelay = TimeSpan.FromMilliseconds(m);
    }
}