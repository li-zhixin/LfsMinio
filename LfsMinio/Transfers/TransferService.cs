using LfsMinio.Infra;
using LfsMinio.Protocol;
using LfsMinio.Storage;
using Microsoft.Extensions.Logging;

namespace LfsMinio.Transfers;

public sealed class TransferService(
    IStorageClient storage,
    IResponder responder,
    IRetryPolicy retry,
    ILogger<TransferService> logger)
    : ITransferService
{
    private readonly List<string> _tmpFiles = [];

    public async Task UploadAsync(UploadEvent evt, CancellationToken ct)
    {
        try
        {
            await retry.ExecuteAsync(async token =>
            {
                await using var fs = File.OpenRead(evt.Path);
                await using var ps = new ProgressStream(fs, responder, evt.Oid, evt.Size);
                await storage.UploadAsync(evt.Oid, ps, evt.Size, token);
            }, ct);

            await responder.CompleteOkAsync(evt.Oid, null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Upload failed for {Oid}", evt.Oid);
            await responder.CompleteErrorAsync(evt.Oid, ex.Message, ct);
        }
    }

    public async Task DownloadAsync(DownloadEvent evt, CancellationToken ct)
    {
        var tmp = Path.Combine(Directory.GetCurrentDirectory(), $".lfs-dl-{Guid.NewGuid():N}");
        try
        {
            await retry.ExecuteAsync(async token =>
            {
                await storage.DownloadAsync(evt.Oid, async stream =>
                {
                    await using var ps = new ProgressStream(stream, responder, evt.Oid, evt.Size);
                    await using var of = File.Create(tmp);
                    await ps.CopyToAsync(of, 81920, token);
                }, token);
            }, ct);

            lock (_tmpFiles) _tmpFiles.Add(tmp);
            await responder.CompleteOkAsync(evt.Oid, tmp, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download failed for {Oid}", evt.Oid);
            await responder.CompleteErrorAsync(evt.Oid, ex.Message, ct);
            TryDelete(tmp);
        }
    }

    public Task CleanupAsync(CancellationToken ct)
    {
        string[] files;
        lock (_tmpFiles) files = _tmpFiles.ToArray();
        foreach (var f in files)
        {
            TryDelete(f);
        }
        return Task.CompletedTask;
    }

    private void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }
}
