namespace Replicant;

public partial class HttpCache
{
    /// <inheritdoc/>
    public virtual void AddItem(string uri, HttpResponseMessage response, Cancel cancel = default) =>
        AddItem(response, new(uri), cancel);

    /// <inheritdoc/>
    public virtual void AddItem(Uri uri, HttpResponseMessage response, Cancel cancel = default) =>
        AddItem(response, uri, cancel);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(string uri, HttpResponseMessage response, Cancel cancel = default) =>
        AddItemAsync(response, new(uri), cancel);

    /// <inheritdoc/>
    public virtual Task AddItemAsync(Uri uri, HttpResponseMessage response, Cancel cancel = default) =>
        AddItemAsync(response, uri, cancel);

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
        Cancel cancel = default) =>
        AddItemAsync(new Uri(uri), stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, cancel);

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
        Cancel cancel = default)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await AddItemAsync(uri, stream, expiry, modified, etag, responseHeaders, contentHeaders, trailingHeaders, cancel);
    }
}