using LfsMinio.Protocol;

namespace LfsMinio.Transfers;

public sealed class ProgressStream(Stream inner, IResponder responder, string oid, long size, TimeSpan? interval = null)
    : Stream
{
    private readonly long _size = size;
    private long _soFar;
    private long _sinceLast;
    private DateTime _last = DateTime.UtcNow;
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromSeconds(1);

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        OnProgress(n, n == 0);
        return n;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var n = await inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        await OnProgressAsync(n, n == 0, cancellationToken);
        return n;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var n = await inner.ReadAsync(buffer, cancellationToken);
        await OnProgressAsync(n, n == 0, cancellationToken);
        return n;
    }

    private void OnProgress(int n, bool eof)
    {
        _soFar += n;
        _sinceLast += n;
        if (DateTime.UtcNow - _last < _interval && !eof)
        {
            return;
        }

        responder.ProgressAsync(oid, _soFar, _sinceLast);
        _last = DateTime.UtcNow;
        _sinceLast = 0;
    }

    private async Task OnProgressAsync(int n, bool eof, CancellationToken ct)
    {
        _soFar += n;
        _sinceLast += n;
        if (DateTime.UtcNow - _last >= _interval || eof)
        {
            await responder.ProgressAsync(oid, _soFar, _sinceLast, ct);
            _last = DateTime.UtcNow;
            _sinceLast = 0;
        }
    }

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();

    public override int Read(Span<byte> buffer)
    {
        var n = inner.Read(buffer);
        OnProgress(n, n == 0);
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        OnProgress(count, false);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
        OnProgress(buffer.Length, false);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer, offset, count, cancellationToken);
        await OnProgressAsync(count, false, cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        await OnProgressAsync(buffer.Length, false, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Report final progress on disposal
            if (_sinceLast > 0)
            {
                responder.ProgressAsync(oid, _soFar, _sinceLast);
            }

            inner?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        // Report final progress on disposal
        if (_sinceLast > 0)
        {
            await responder.ProgressAsync(oid, _soFar, _sinceLast);
        }
        await inner.DisposeAsync();
        await base.DisposeAsync();
    }
}