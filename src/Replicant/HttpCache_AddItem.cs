namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void AddItem(string uri, HttpResponseMessage response, CancellationToken token = default) =>
        AddItem(response, new(uri), token);

    /// <inheritdoc/>
    public virtual void AddItem(Uri uri, HttpResponseMessage response, CancellationToken token = default) =>
        AddItem(response, uri, token);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(string uri, HttpResponseMessage response, CancellationToken token = default) =>
        AddItemAsync(response, new(uri), token);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(Uri uri, HttpResponseMessage response, CancellationToken token = default) =>
        AddItemAsync(response, uri, token);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(
        string uri,
        Stream stream,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        CancellationToken token = default) =>
        AddItemAsync(new Uri(uri), stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, token);

    /// <inheritdoc/>
    public virtual async Task AddItemAsync(
        string uri,
        string content,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        CancellationToken token = default)
    {
        using var stream = content.AsStream();
        await AddItemAsync(uri, stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, token);
    }
}