namespace Replicant;

public interface IHttpCache:
    IDisposable,
    IAsyncDisposable
{
    /// <summary>
    /// Download a resource and store the result in <paramref name="path"/>.
    /// </summary>
    Task ToFileAsync(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="path"/>.
    /// </summary>
    Task ToFileAsync(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="path"/>.
    /// </summary>
    void ToFile(
        string uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="path"/>.
    /// </summary>
    void ToFile(
        Uri uri,
        string path,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    void AddItem(string uri, HttpResponseMessage response, CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    void AddItem(Uri uri, HttpResponseMessage response, CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    Task AddItemAsync
    (string uri,
        HttpResponseMessage response,
        CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    Task AddItemAsync(
        Uri uri,
        HttpResponseMessage response,
        CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    Task AddItemAsync(
        string uri,
        Stream stream,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    Task AddItemAsync(
        string uri,
        string content,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        CancellationToken token = default);

    /// <summary>
    /// Manually add an item to the cache.
    /// </summary>
    Task AddItemAsync(
        Uri uri,
        Stream stream,
        DateTimeOffset? expiry = null,
        DateTimeOffset? modified = null,
        string? etag = null,
        Headers? responseHeaders = null,
        Headers? contentHeaders = null,
        Headers? trailingHeaders = null,
        CancellationToken token = default);

    /// <summary>
    /// Purge all items from the cache.
    /// </summary>
    void Purge();

    /// <summary>
    /// Purge old items based on maxEntries.
    /// </summary>
    void PurgeOld();

    /// <summary>
    /// Download a resource and return the result as <see cref="System.IO.Stream"/>.
    /// </summary>
    Task<Stream> StreamAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="System.IO.Stream"/>.
    /// </summary>
    Task<Stream> StreamAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="System.IO.Stream"/>.
    /// </summary>
    Stream Stream(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="System.IO.Stream"/>.
    /// </summary>
    Stream Stream(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="stream"/>.
    /// </summary>
    Task ToStreamAsync(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="stream"/>.
    /// </summary>
    Task ToStreamAsync(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="stream"/>.
    /// </summary>
    void ToStream(
        string uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and store the result in <paramref name="stream"/>.
    /// </summary>
    void ToStream(
        Uri uri,
        Stream stream,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a string.
    /// </summary>
    Task<string> StringAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a string.
    /// </summary>
    Task<string> StringAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a string.
    /// </summary>
    string String(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a string.
    /// </summary>
    string String(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    IAsyncEnumerable<string> LinesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    IAsyncEnumerable<string> LinesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    IEnumerable<string> Lines(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    IEnumerable<string> Lines(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="HttpResponseMessage"/>.
    /// </summary>
    Task<HttpResponseMessage> ResponseAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="HttpResponseMessage"/>.
    /// </summary>
    Task<HttpResponseMessage> ResponseAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="HttpResponseMessage"/>.
    /// </summary>
    Task<HttpResponseMessage> Response(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as <see cref="HttpResponseMessage"/>.
    /// </summary>
    Task<HttpResponseMessage> Response(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as byte array.
    /// </summary>
    Task<byte[]> BytesAsync(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as byte array.
    /// </summary>
    Task<byte[]> BytesAsync(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a byte array.
    /// </summary>
    byte[] Bytes(
        string uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);

    /// <summary>
    /// Download a resource and return the result as a byte array.
    /// </summary>
    byte[] Bytes(
        Uri uri,
        bool staleIfError = false,
        Action<HttpRequestMessage>? modifyRequest = null,
        CancellationToken token = default);
}