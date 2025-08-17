using LfsMinio.Protocol;

namespace LfsMinio.Transfers;

public interface ITransferService
{
    Task UploadAsync(string repo, UploadEvent evt, CancellationToken ct);
    Task DownloadAsync(string repo, DownloadEvent evt, CancellationToken ct);
    Task CleanupAsync(CancellationToken ct);
}

