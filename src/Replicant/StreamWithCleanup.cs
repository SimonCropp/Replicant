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

    public override void EndWrite(IAsyncResult asyncResult) =>
        inner.EndWrite(asyncResult);

    public override Task WriteAsync(byte[] buffer, int offset, int count, Cancellation cancellation) =>
        inner.WriteAsync(buffer, offset, count, cancellation);

    public override void WriteByte(byte value) =>
        inner.WriteByte(value);

    public override int WriteTimeout
    {
        get => inner.WriteTimeout;
        set => inner.WriteTimeout = value;
    }

    public override int GetHashCode() =>
        inner.GetHashCode();

    public override bool Equals(object? obj) =>
        inner.Equals(obj);

    public override string? ToString() =>
        inner.ToString();

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, Cancellation cancellation) =>
        inner.ReadAsync(buffer, offset, count, cancellation);

    public override int ReadTimeout
    {
        get => inner.ReadTimeout;
        set => inner.ReadTimeout = value;
    }

#if NET7_0_OR_GREATER

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, Cancellation cancellation = default) =>
        inner.WriteAsync(buffer, cancellation);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, Cancellation cancellation = default) =>
        inner.ReadAsync(buffer, cancellation);

    public override void Write(ReadOnlySpan<byte> buffer) =>
        inner.Write(buffer);

    public override int Read(Span<byte> buffer) =>
        inner.Read(buffer);

    public override void CopyTo(Stream destination, int bufferSize) =>
        inner.CopyTo(destination, bufferSize);

    public override async ValueTask DisposeAsync()
    {
        await inner.DisposeAsync();
        disposable.Dispose();
        await base.DisposeAsync();
    }

#endif

    public override Task FlushAsync(Cancellation cancellation) =>
        inner.FlushAsync(cancellation);

    public override bool CanTimeout => inner.CanTimeout;

    public override int EndRead(IAsyncResult asyncResult) =>
        inner.EndRead(asyncResult);

    public override void Flush() =>
        inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        inner.Seek(offset, origin);

    public override void SetLength(long value) =>
        inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        inner.Write(buffer, offset, count);

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

    public override int ReadByte() =>
        inner.ReadByte();

    public override Task CopyToAsync(Stream destination, int bufferSize, Cancellation cancellation) =>
        inner.CopyToAsync(destination, bufferSize, cancellation);

    public override void Close()
    {
        inner.Close();
        base.Close();
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        inner.BeginRead(buffer, offset, count, callback, state);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
        inner.BeginWrite(buffer, offset, count, callback, state);
}