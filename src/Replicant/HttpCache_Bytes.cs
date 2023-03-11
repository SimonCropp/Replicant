namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual async Task<byte[]> BytesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancellation);
        return await result.AsBytesAsync(cancellation);
    }

    /// <inheritdoc/>
    public virtual Task<byte[]> BytesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        BytesAsync(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        Bytes(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancellation);
        return result.AsBytes();
    }
}