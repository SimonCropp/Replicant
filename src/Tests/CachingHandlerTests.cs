// ReSharper disable ShortLivedHttpClient
// ReSharper disable UnusedVariable

using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class CachingHandlerTests
{
    static string cachePath = Path.Combine(Path.GetTempPath(), "ReplicantHandlerTests");

    [TearDown]
    public void Cleanup()
    {
        if (Directory.Exists(cachePath))
        {
            Directory.Delete(cachePath, true);
        }
    }

    static async Task ReplicantHandlerUsage(string cacheDirectory)
    {
        #region ReplicantHandlerUsage

        var handler = new ReplicantHandler(cacheDirectory)
        {
            InnerHandler = new HttpClientHandler()
        };
        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://example.com");

        #endregion
    }

    static void HttpClientFactoryUsage(string cacheDirectory)
    {
        #region HttpClientFactoryUsage

        var services = new ServiceCollection();
        services.AddHttpClient("CachedClient")
            .AddHttpMessageHandler(() => new ReplicantHandler(cacheDirectory));

        #endregion
    }

    static void HttpClientFactorySharedCacheUsage(string cacheDirectory)
    {
        #region HttpClientFactorySharedCacheUsage

        var services = new ServiceCollection();
        services.AddReplicantCache(cacheDirectory);
        services.AddHttpClient("CachedClient")
            .AddReplicantCaching();

        #endregion
    }

    [Test]
    public async Task BasicCaching()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello")
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        var content1 = await client.GetStringAsync("http://example.com/test");
        AreEqual("hello", content1);

        // Second request served from cache (mock has no more responses)
        var content2 = await client.GetStringAsync("http://example.com/test");
        AreEqual("hello", content2);

        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HeadRequest()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("")
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        using var request1 = new HttpRequestMessage(HttpMethod.Head, "http://example.com/head");
        using var response1 = await client.SendAsync(request1);
        AreEqual(HttpStatusCode.OK, response1.StatusCode);

        // Second HEAD served from cache
        using var request2 = new HttpRequestMessage(HttpMethod.Head, "http://example.com/head");
        using var response2 = await client.SendAsync(request2);
        AreEqual(HttpStatusCode.OK, response2.StatusCode);
    }

    [Test]
    public async Task CacheHit_NotModified()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("original content")
            },
            new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = new StringContent("")
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/revalidate");
        AreEqual("original content", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(cachePath, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: revalidation returns 304, cached content served
        var content2 = await client.GetStringAsync("http://example.com/revalidate");
        AreEqual("original content", content2);
    }

    [Test]
    public async Task NoStore()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("no-store content"),
                Headers =
                {
                    CacheControl = new()
                    {
                        NoStore = true
                    }
                }
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/nostore");
        AreEqual("no-store content", content);

        // No files should be cached
        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task StaleIfError()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("cached content"),
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("error")
            });
        using var handler = new ReplicantHandler(cachePath, inner, staleIfError: true);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/stale");
        AreEqual("cached content", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(cachePath, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: server error, stale content returned
        var content2 = await client.GetStringAsync("http://example.com/stale");
        AreEqual("cached content", content2);
    }

    [Test]
    public async Task NonGetPostPassthrough()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("post response")
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        var response = await client.PostAsync(
            "http://example.com/post",
            new StringContent("body"));
        var content = await response.Content.ReadAsStringAsync();
        AreEqual("post response", content);

        // POST should not be cached
        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task Purge()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("to purge")
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        await client.GetStringAsync("http://example.com/purge");
        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(1, binFiles.Length);

        handler.Purge();

        binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task NoCache_StoresButRevalidates()
    {
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("content"),
                Headers =
                {
                    CacheControl = new()
                    {
                        NoCache = true
                    }
                }
            });
        using var handler = new ReplicantHandler(cachePath, inner);
        using var client = new HttpClient(handler);

        // no-cache responses are still stored
        var content = await client.GetStringAsync("http://example.com/nocache");
        AreEqual("content", content);

        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_NamedClient()
    {
        using var cache = new ReplicantCache(cachePath);
        var services = new ServiceCollection();
        services.AddSingleton(cache);
        services.AddHttpClient("CachedClient")
            .ConfigurePrimaryHttpMessageHandler(
                () => new MockHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("factory content")
                    }))
            .AddHttpMessageHandler(
                p => new ReplicantHandler(p.GetRequiredService<ReplicantCache>()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("CachedClient");

        var content1 = await client.GetStringAsync("http://example.com/factory");
        AreEqual("factory content", content1);

        // Second request served from cache
        var content2 = await client.GetStringAsync("http://example.com/factory");
        AreEqual("factory content", content2);

        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_SharedCache()
    {
        using var cache = new ReplicantCache(cachePath);
        var services = new ServiceCollection();
        services.AddSingleton(cache);
        services.AddHttpClient("Client1")
            .ConfigurePrimaryHttpMessageHandler(
                () => new MockHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("shared content")
                    }))
            .AddHttpMessageHandler(
                p => new ReplicantHandler(p.GetRequiredService<ReplicantCache>()));
        services.AddHttpClient("Client2")
            .ConfigurePrimaryHttpMessageHandler(() => new MockHttpMessageHandler())
            .AddHttpMessageHandler(
                p => new ReplicantHandler(p.GetRequiredService<ReplicantCache>()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        // Client1 fetches and caches
        using var client1 = factory.CreateClient("Client1");
        var content1 = await client1.GetStringAsync("http://example.com/shared");
        AreEqual("shared content", content1);

        // Client2 serves from the shared cache (mock has no responses)
        using var client2 = factory.CreateClient("Client2");
        var content2 = await client2.GetStringAsync("http://example.com/shared");
        AreEqual("shared content", content2);

        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_NonGetPassthrough()
    {
        using var cache = new ReplicantCache(cachePath);
        var services = new ServiceCollection();
        services.AddSingleton(cache);
        services.AddHttpClient("CachedClient")
            .ConfigurePrimaryHttpMessageHandler(
                () => new MockHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("post response")
                    }))
            .AddHttpMessageHandler(
                p => new ReplicantHandler(p.GetRequiredService<ReplicantCache>()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("CachedClient");

        var response = await client.PostAsync(
            "http://example.com/post",
            new StringContent("body"));
        var content = await response.Content.ReadAsStringAsync();
        AreEqual("post response", content);

        // POST should not be cached
        var binFiles = Directory.GetFiles(cachePath, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public void DuplicateDirectory_Throws()
    {
        using var handler1 = new ReplicantHandler(cachePath, new MockHttpMessageHandler());

        Assert.Throws<InvalidOperationException>(
            () => new ReplicantHandler(cachePath, new MockHttpMessageHandler()));
    }

    [Test]
    public void DuplicateDirectory_AfterDispose_Allowed()
    {
        var handler1 = new ReplicantHandler(cachePath, new MockHttpMessageHandler());
        handler1.Dispose();

        // After dispose, same directory can be reused
        using var handler2 = new ReplicantHandler(cachePath, new MockHttpMessageHandler());
    }

    [Test]
    public void DuplicateDirectory_SharedCache_Throws()
    {
        using var cache = new ReplicantCache(cachePath);

        Assert.Throws<InvalidOperationException>(
            () => new ReplicantCache(cachePath));
    }

    [Test]
    public void DuplicateDirectory_SharedCache_AfterDispose_Allowed()
    {
        var cache1 = new ReplicantCache(cachePath);
        cache1.Dispose();

        using var cache2 = new ReplicantCache(cachePath);
    }

    [Test]
    public void DuplicateDirectory_HandlerAndCache_Throws()
    {
        using var handler = new ReplicantHandler(cachePath, new MockHttpMessageHandler());

        Assert.Throws<InvalidOperationException>(
            () => new ReplicantCache(cachePath));
    }

    [Test]
    public void DuplicateDirectory_SharedCacheHandler_DoesNotThrow()
    {
        // Multiple handlers sharing a ReplicantCache should not throw
        using var cache = new ReplicantCache(cachePath);
        using var handler1 = new ReplicantHandler(cache);
        using var handler2 = new ReplicantHandler(cache);
    }

    [Test]
    public Task AddReplicantCache_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        services.AddReplicantCache(cachePath);

        return Throws(() => services.AddReplicantCache(cachePath));
    }

    [Test]
    public Task AddReplicantCaching_WithoutCache_Throws()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("CachedClient")
            .AddReplicantCaching();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        return Throws(() => factory.CreateClient("CachedClient"));
    }
}