using System.Net.Http;

namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> ResponseAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return ResponseAsync(new Uri(uri), staleIfError, modifyRequest, token);
    }

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> ResponseAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return result.AsResponseMessage();
    }

    /// <inheritdoc/>
    public virtual Task<HttpResponseMessage> Response(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        return Response(new Uri(uri), staleIfError, modifyRequest, token);
    }

    /// <inheritdoc/>
    public virtual async Task<HttpResponseMessage> Response(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, token);
        return result.AsResponseMessage();
    }
}