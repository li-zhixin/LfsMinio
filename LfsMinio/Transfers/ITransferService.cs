using LfsMinio.Protocol;

namespace LfsMinio.Transfers;

public interface ITransferService
{
    Task UploadAsync(UploadEvent evt, CancellationToken ct);
    Task DownloadAsync(DownloadEvent evt, CancellationToken ct);
    Task CleanupAsync(CancellationToken ct);
}

