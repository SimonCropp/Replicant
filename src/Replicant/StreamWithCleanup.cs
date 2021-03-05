using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class StreamWithCleanup :
    Stream
{
    Stream inner;
    IDisposable disposable;

    public StreamWithCleanup(Stream inner, IDisposable disposable)
    {
        this.inner = inner;
        this.disposable = disposable;
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        inner.EndWrite(asyncResult);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override void WriteByte(byte value)
    {
        inner.WriteByte(value);
    }

    public override int WriteTimeout
    {
        get => inner.WriteTimeout;
        set => inner.WriteTimeout = value;
    }

    public override int GetHashCode()
    {
        return inner.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return inner.Equals(obj);
    }

    public override string ToString()
    {
        return inner.ToString()!;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override int ReadTimeout
    {
        get => inner.ReadTimeout;
        set => inner.ReadTimeout = value;
    }

#if !NET472 && !NETSTANDARD2_0

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return inner.WriteAsync(buffer, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return inner.ReadAsync(buffer, cancellationToken);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
    }

    public override int Read(Span<byte> buffer)
    {
        return inner.Read(buffer);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        inner.CopyTo(destination, bufferSize);
    }

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        disposable.Dispose();
        await base.DisposeAsync();
    }

#endif

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override bool CanTimeout
    {
        get => inner.CanTimeout;
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return inner.EndRead(asyncResult);
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return inner.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
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

    protected override void Dispose(bool disposing)
    {
        inner.Dispose();
        disposable.Dispose();
        base.Dispose(disposing);
    }

    public override int ReadByte()
    {
        return inner.ReadByte();
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return inner.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override void Close()
    {
        inner.Close();
        base.Close();
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return inner.BeginRead(buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return inner.BeginWrite(buffer, offset, count, callback, state);
    }
}