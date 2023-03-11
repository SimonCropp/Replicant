namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<string> StringAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        StringAsync(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual async Task<string> StringAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancellation);
        return await result.AsStringAsync(cancellation);
    }

    /// <inheritdoc/>
    public virtual string String(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        String(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual string String(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancellation);
        return result.AsString();
    }
}