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
    public string? RepoName { get; set; } // repo identifier for S3 path separation

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
        o.RepoName = Environment.GetEnvironmentVariable("LFS_REPO_NAME");

        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_MAX_ATTEMPTS"), out var r) && r > 0)
            o.RetryMaxAttempts = r;

        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_BASE_MS"), out var b) && b > 0)
            o.RetryBaseDelay = TimeSpan.FromMilliseconds(b);

        if (int.TryParse(Environment.GetEnvironmentVariable("LFS_RETRY_MAX_MS"), out var m) && m > 0)
            o.RetryMaxDelay = TimeSpan.FromMilliseconds(m);
    }

    public static void OverrideFromArgs(AppOptions o, string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--"))
            {
                var keyValue = arg[2..]; // Remove "--"
                var equalIndex = keyValue.IndexOf('=');
                if (equalIndex > 0)
                {
                    var key = keyValue[..equalIndex];
                    var value = keyValue[(equalIndex + 1)..];
                    
                    switch (key.ToLowerInvariant())
                    {
                        case "bucket":
                            o.Bucket = value;
                            break;
                        case "endpoint":
                            o.Endpoint = value;
                            break;
                        case "access-key":
                            o.AccessKey = value;
                            break;
                        case "secret-key":
                            o.SecretKey = value;
                            break;
                        case "region":
                            o.Region = value;
                            break;
                        case "repo":
                        case "repo-name":
                            o.RepoName = value;
                            break;
                        case "secure":
                            o.Secure = value != "0" && value.ToLowerInvariant() != "false";
                            break;
                        case "retry-max-attempts":
                            if (int.TryParse(value, out var retryMax) && retryMax > 0)
                                o.RetryMaxAttempts = retryMax;
                            break;
                        case "retry-base-delay":
                            if (int.TryParse(value, out var baseDelay) && baseDelay > 0)
                                o.RetryBaseDelay = TimeSpan.FromMilliseconds(baseDelay);
                            break;
                        case "retry-max-delay":
                            if (int.TryParse(value, out var maxDelay) && maxDelay > 0)
                                o.RetryMaxDelay = TimeSpan.FromMilliseconds(maxDelay);
                            break;
                    }
                }
            }
        }
    }
}