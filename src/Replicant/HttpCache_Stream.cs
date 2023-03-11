namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual Task<Stream> StreamAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        StreamAsync(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual async Task<Stream> StreamAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancellation);
        return await result.AsStreamAsync(cancellation);
    }

    /// <inheritdoc/>
    public virtual Stream Stream(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        Stream(new Uri(uri), staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual Stream Stream(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        var result = Download(uri, staleIfError, modifyRequest, cancellation);
        return result.AsStream(cancellation);
    }

    /// <inheritdoc/>
    public virtual Task ToStreamAsync(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        ToStreamAsync(new Uri(uri), stream, staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual async Task ToStreamAsync(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = await DownloadAsync(uri, staleIfError, modifyRequest, cancellation);
        await result.ToStreamAsync(stream, cancellation);
    }

    /// <inheritdoc/>
    public virtual void ToStream(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default) =>
        ToStream(new Uri(uri), stream, staleIfError, modifyRequest, cancellation);

    /// <inheritdoc/>
    public virtual void ToStream(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        Cancellation cancellation = default)
    {
        using var result = Download(uri, staleIfError, modifyRequest, cancellation);
        result.ToStream(stream, cancellation);
    }
}