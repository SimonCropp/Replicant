using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using BenchmarkDotNet.Attributes;
using Replicant;

[MemoryDiagnoser]
public class HandlerBenchmarks
{
    static readonly Uri uri = new("http://example.com/resource");
    string cacheDir = null!;
    ReplicantHandler handler = null!;
    HttpClient client = null!;

    [GlobalSetup]
    public void Setup()
    {
        cacheDir = Path.Combine(Path.GetTempPath(), "ReplicantBench_Handler", Guid.NewGuid().ToString());

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("benchmark-response-content"u8.ToArray())
        };
        response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };

        var mockHandler = new MockHttpMessageHandler(response);
        handler = new ReplicantHandler(cacheDir, mockHandler);
        client = new HttpClient(handler);

        // Prime the cache
        client.GetStringAsync(uri).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        client.Dispose();

        if (Directory.Exists(cacheDir))
        {
            Directory.Delete(cacheDir, true);
        }
    }

    [Benchmark]
    public Task<HttpResponseMessage> SendAsync_Hit() =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Get, uri));

    [Benchmark]
    public HttpResponseMessage Send_Hit() =>
        client.Send(new HttpRequestMessage(HttpMethod.Get, uri));

    [Benchmark]
    public Task<string> GetStringAsync_Hit() =>
        client.GetStringAsync(uri);

    [Benchmark]
    public Task<byte[]> GetByteArrayAsync_Hit() =>
        client.GetByteArrayAsync(uri);
}
