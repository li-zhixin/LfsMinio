using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LfsMinio.Protocol;

public interface IResponder
{
    Task WriteAsync(LfsResponse response, CancellationToken ct = default);
    Task ProgressAsync(string oid, long soFar, long sinceLast, CancellationToken ct = default)
        => WriteAsync(ProgressResponse.Create(oid, soFar, sinceLast), ct);
    Task CompleteOkAsync(string oid, string? path = null, CancellationToken ct = default)
        => WriteAsync(CompleteResponse.Ok(oid, path), ct);
    Task CompleteErrorAsync(string oid, string message, CancellationToken ct = default)
        => WriteAsync(CompleteResponse.Fail(oid, message), ct);
}

public sealed class LfsResponder : IResponder
{
    private readonly ILogger<LfsResponder> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Stream _output;
    private readonly StreamWriter _writer;

    public LfsResponder(ILogger<LfsResponder> logger)
    {
        _logger = logger;
        _output = Console.OpenStandardOutput();
        _writer = new StreamWriter(_output, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public async Task WriteAsync(LfsResponse response, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var json = JsonSerializer.Serialize(response switch
            {
                InitOkResponse => new object(),
                InitErrorResponse err => new { error = new { code = 1, message = err.Message } },
                var other => other
            }, JsonOpts);

            _logger.LogDebug("-> {Json}", json);
            await _writer.WriteLineAsync(json);
        }
        finally
        {
            _mutex.Release();
        }
    }
}

