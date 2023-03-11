namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> ResponseAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        ResponseAsync(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> ResponseAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return result.AsResponseMessage();
    }

    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> Response(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        Response(new Uri(uri), staleIfError, modifyRequest, token);

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> Response(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return result.AsResponseMessage();
    }
}