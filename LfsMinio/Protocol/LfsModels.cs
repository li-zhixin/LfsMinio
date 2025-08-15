using System.Text.Json.Serialization;

namespace LfsMinio.Protocol;

public abstract record LfsEvent;

public sealed record InitEvent(
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("remote")] string? Remote,
    [property: JsonPropertyName("concurrent")] bool Concurrent,
    [property: JsonPropertyName("concurrenttransfers")] int ConcurrentTransfers
) : LfsEvent;

public sealed record UploadEvent(
    [property: JsonPropertyName("oid")] string Oid,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("path")] string Path
) : LfsEvent;

public sealed record DownloadEvent(
    [property: JsonPropertyName("oid")] string Oid,
    [property: JsonPropertyName("size")] long Size
) : LfsEvent;

public sealed record TerminateEvent() : LfsEvent;

public abstract record LfsResponse;

public sealed record InitOkResponse() : LfsResponse;

public sealed record InitErrorResponse(string Message) : LfsResponse;

public sealed record ProgressResponse(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("oid")] string Oid,
    [property: JsonPropertyName("bytesSoFar")] long BytesSoFar,
    [property: JsonPropertyName("bytesSinceLast")] long BytesSinceLast
) : LfsResponse
{
    public static ProgressResponse Create(string oid, long soFar, long sinceLast) => new("progress", oid, soFar, sinceLast);
}

public sealed record CompleteResponse(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("oid")] string Oid,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("error")] LfsError? Error
) : LfsResponse
{
    public static CompleteResponse Ok(string oid, string? path = null) => new("complete", oid, path, null);
    public static CompleteResponse Fail(string oid, string message) => new("complete", oid, null, new LfsError(1, message));
}

public sealed record LfsError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message
);

