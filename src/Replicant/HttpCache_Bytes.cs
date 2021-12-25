namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual async Task<byte[]> BytesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return await result.AsBytesAsync(token);
    }

    /// <inheritdoc/>
    public virtual Task<byte[]> BytesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return BytesAsync(new Uri(uri), staleIfError, modifyRequest, token);
    }

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return Bytes(new Uri(uri), staleIfError, modifyRequest, token);
    }

    /// <inheritdoc/>
    public virtual byte[] Bytes(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, token);
        return result.AsBytes();
    }
}