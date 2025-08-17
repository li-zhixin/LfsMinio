using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LfsMinio.Protocol;

public interface ILfsReader
{
    IAsyncEnumerable<LfsEvent> ReadEventsAsync(Stream input, CancellationToken ct = default);
}

public sealed class LfsReader(ILogger<LfsReader> logger) : ILfsReader
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = LfsJsonContext.Default
    };

    public async IAsyncEnumerable<LfsEvent> ReadEventsAsync(Stream input, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
        while (!ct.IsCancellationRequested && await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            LfsEvent? result = null;
            try
            {
                logger.LogDebug("<- {Line}", line);

                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("event", out var evtProp))
                {
                    var evtName = evtProp.GetString();
                    switch (evtName)
                    {
                        case "init":
                            result = JsonSerializer.Deserialize<InitEvent>(line, JsonOpts)!;
                            break;
                        case "upload":
                            result = JsonSerializer.Deserialize<UploadEvent>(line, JsonOpts)!;
                            break;
                        case "download":
                            result = JsonSerializer.Deserialize<DownloadEvent>(line, JsonOpts)!;
                            break;
                        case "terminate":
                            result = new TerminateEvent();
                            break;
                        default:
                            logger.LogWarning("Unknown LFS event: {Event}", evtName);
                            break;
                    }
                }
                else if (doc.RootElement.TryGetProperty("operation", out _))
                {
                    // Some git-lfs send init without event field but with operation
                    result = JsonSerializer.Deserialize<InitEvent>(line, JsonOpts)!;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse LFS event line");
            }
            
            if (result != null)
                yield return result;
        }
    }
}

