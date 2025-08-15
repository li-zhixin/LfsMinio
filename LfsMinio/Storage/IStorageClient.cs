namespace LfsMinio.Storage;

public interface IStorageClient
{
    Task UploadAsync(string oid, Stream content, long size, CancellationToken ct);
    Task DownloadAsync(string oid, Func<Stream, Task> handleStreamAsync, CancellationToken ct);
    Task ValidateConnectionAsync(CancellationToken ct = default);
}
