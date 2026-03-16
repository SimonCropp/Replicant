using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using BenchmarkDotNet.Attributes;
using Replicant;

[MemoryDiagnoser]
public class HttpCacheBenchmarks
{
    static readonly Uri uri = new("http://example.com/resource");
    string cacheDir = null!;
    HttpCache cache = null!;

    [GlobalSetup]
    public void Setup()
    {
        cacheDir = Path.Combine(Path.GetTempPath(), "ReplicantBench_HttpCache", Guid.NewGuid().ToString());

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("benchmark-response-content"u8.ToArray())
        };
        response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };

        var client = new MockHttpClient(response);
        cache = new HttpCache(cacheDir, client);

        // Prime the cache
        cache.StringAsync(uri).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        cache.Dispose();

        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Benchmark]
    public Task<string> StringAsync_Hit() =>
        cache.StringAsync(uri);

    [Benchmark]
    public string String_Hit() =>
        cache.String(uri);

    [Benchmark]
    public Task<byte[]> BytesAsync_Hit() =>
        cache.BytesAsync(uri);

    [Benchmark]
    public byte[] Bytes_Hit() =>
        cache.Bytes(uri);

    [Benchmark]
    public async Task<Stream> StreamAsync_Hit() =>
        await cache.StreamAsync(uri);

    [Benchmark]
    public Stream Stream_Hit() =>
        cache.Stream(uri);
}
