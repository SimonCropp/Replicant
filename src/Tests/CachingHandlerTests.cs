// ReSharper disable ShortLivedHttpClient
// ReSharper disable UnusedVariable

using Microsoft.Extensions.DependencyInjection;

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class CachingHandlerTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantHandlerTests");

    static string CachePath([CallerMemberName] string name = "") =>
        Path.Combine(root, name);

    [OneTimeTearDown]
    public void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
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
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("hello")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        var content1 = await client.GetStringAsync("http://example.com/test");
        AreEqual("hello", content1);

        // Second request served from cache (mock has no more responses)
        var content2 = await client.GetStringAsync("http://example.com/test");
        AreEqual("hello", content2);

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HeadRequest()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("")
            });
        using var handler = new ReplicantHandler(path, inner);
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
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("original content")
            },
            new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = new StringContent("")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/revalidate");
        AreEqual("original content", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: revalidation returns 304, cached content served
        var content2 = await client.GetStringAsync("http://example.com/revalidate");
        AreEqual("original content", content2);
    }

    [Test]
    public async Task NoStore()
    {
        var path = CachePath();
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
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/nostore");
        AreEqual("no-store content", content);

        // No files should be cached
        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task StaleIfError()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("cached content"),
            },
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("error")
            });
        using var handler = new ReplicantHandler(path, inner, staleIfError: true);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/stale");
        AreEqual("cached content", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: server error, stale content returned
        var content2 = await client.GetStringAsync("http://example.com/stale");
        AreEqual("cached content", content2);
    }

    [Test]
    public async Task NonGetPostPassthrough()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("post response")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        var response = await client.PostAsync(
            "http://example.com/post",
            new StringContent("body"));
        var content = await response.Content.ReadAsStringAsync();
        AreEqual("post response", content);

        // POST should not be cached
        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task Purge()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("to purge")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        await client.GetStringAsync("http://example.com/purge");
        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);

        handler.Purge();

        binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public async Task NoCache_StoresButRevalidates()
    {
        var path = CachePath();
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
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        // no-cache responses are still stored
        var content = await client.GetStringAsync("http://example.com/nocache");
        AreEqual("content", content);

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_NamedClient()
    {
        var path = CachePath();
        using var cache = new ReplicantCache(path);
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

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_SharedCache()
    {
        var path = CachePath();
        using var cache = new ReplicantCache(path);
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

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);
    }

    [Test]
    public async Task HttpClientFactory_NonGetPassthrough()
    {
        var path = CachePath();
        using var cache = new ReplicantCache(path);
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
        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(0, binFiles.Length);
    }

    [Test]
    public void DuplicateDirectory_Throws()
    {
        var path = CachePath();
        using var handler1 = new ReplicantHandler(path, new MockHttpMessageHandler());

        Assert.Throws<Exception>(
            () => new ReplicantHandler(path, new MockHttpMessageHandler()));
    }

    [Test]
    public void DuplicateDirectory_AfterDispose_Allowed()
    {
        var path = CachePath();
        var handler1 = new ReplicantHandler(path, new MockHttpMessageHandler());
        handler1.Dispose();

        // After dispose, same directory can be reused
        using var handler2 = new ReplicantHandler(path, new MockHttpMessageHandler());
    }

    [Test]
    public void DuplicateDirectory_SharedCache_Throws()
    {
        var path = CachePath();
        using var cache = new ReplicantCache(path);

        Assert.Throws<Exception>(
            () => new ReplicantCache(path));
    }

    [Test]
    public void DuplicateDirectory_SharedCache_AfterDispose_Allowed()
    {
        var path = CachePath();
        var cache1 = new ReplicantCache(path);
        cache1.Dispose();

        using var cache2 = new ReplicantCache(path);
    }

    [Test]
    public void DuplicateDirectory_HandlerAndCache_Throws()
    {
        var path = CachePath();
        using var handler = new ReplicantHandler(path, new MockHttpMessageHandler());

        Assert.Throws<Exception>(
            () => new ReplicantCache(path));
    }

    [Test]
    public void DuplicateDirectory_SharedCacheHandler_DoesNotThrow()
    {
        var path = CachePath();
        // Multiple handlers sharing a ReplicantCache should not throw
        using var cache = new ReplicantCache(path);
        using var handler1 = new ReplicantHandler(cache);
        using var handler2 = new ReplicantHandler(cache);
    }

    [Test]
    public Task AddReplicantCache_CalledTwice_Throws()
    {
        var path = CachePath();
        var services = new ServiceCollection();
        services.AddReplicantCache(path);

        return Throws(() => services.AddReplicantCache(path));
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

    [Test]
    public async Task Cache404_Stores()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner, cache404: true);
        using var client = new HttpClient(handler);

        // First request: 404 is stored to cache
        using var response1 = await client.GetAsync("http://example.com/missing");
        AreEqual(HttpStatusCode.NotFound, response1.StatusCode);
        var content1 = await response1.Content.ReadAsStringAsync();
        AreEqual("not found", content1);

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);

        // Second request served from cache, status code preserved
        using var response2 = await client.GetAsync("http://example.com/missing");
        AreEqual(HttpStatusCode.NotFound, response2.StatusCode);
        var content2 = await response2.Content.ReadAsStringAsync();
        AreEqual("not found", content2);
    }

    [Test]
    public async Task Cache404_Disabled_Throws()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("http://example.com/missing404"));
    }

    [Test]
    public async Task Cache404_IsSuccessStatusCode_False()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner, cache404: true);
        using var client = new HttpClient(handler);

        // First request: fresh 404 from server, stored to cache
        using var response1 = await client.GetAsync("http://example.com/isSuccess");
        IsFalse(response1.IsSuccessStatusCode);

        // Second request: served from cache, still not successful
        using var response2 = await client.GetAsync("http://example.com/isSuccess");
        IsFalse(response2.IsSuccessStatusCode);
    }

    [Test]
    public async Task Cache404_EnsureSuccessStatusCode_Throws()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner, cache404: true);
        using var client = new HttpClient(handler);

        // First request: fresh 404, stored to cache
        using var response1 = await client.GetAsync("http://example.com/ensureSuccess");
        Assert.Throws<HttpRequestException>(() => response1.EnsureSuccessStatusCode());

        // Second request: cached 404, still throws
        using var response2 = await client.GetAsync("http://example.com/ensureSuccess");
        Assert.Throws<HttpRequestException>(() => response2.EnsureSuccessStatusCode());
    }

    [Test]
    public async Task Cache200_IsSuccessStatusCode_True()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        using var handler = new ReplicantHandler(path, inner, cache404: true);
        using var client = new HttpClient(handler);

        // First request: fresh 200, stored to cache
        using var response1 = await client.GetAsync("http://example.com/success200");
        IsTrue(response1.IsSuccessStatusCode);
        response1.EnsureSuccessStatusCode();

        // Second request: served from cache, still successful
        using var response2 = await client.GetAsync("http://example.com/success200");
        IsTrue(response2.IsSuccessStatusCode);
        response2.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task MinFreshness_SkipsRevalidation()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("original content")
            },
            // If revalidation happens, this would change the content
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("updated content")
            });
        using var handler = new ReplicantHandler(path, inner, minFreshness: TimeSpan.FromHours(1));
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/minfresh");
        AreEqual("original content", content1);

        // Expire the cached file (server expiry in the past)
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: expired per server headers, but minFreshness keeps it fresh
        var content2 = await client.GetStringAsync("http://example.com/minfresh");
        AreEqual("original content", content2);
    }

    [Test]
    public async Task MinFreshness_RevalidatesWhenStale()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("original content")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("updated content")
            });
        using var handler = new ReplicantHandler(path, inner, minFreshness: TimeSpan.FromMilliseconds(1));
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/minfreshstale");
        AreEqual("original content", content1);

        // Expire the cached file and wait for minFreshness to elapse
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));
        File.SetCreationTimeUtc(binFile, new(2020, 1, 1));
        await Task.Delay(10);

        // Second request: both server expiry and minFreshness elapsed, revalidates
        var content2 = await client.GetStringAsync("http://example.com/minfreshstale");
        AreEqual("updated content", content2);
    }

    [Test]
    public async Task Cache404_SharedCache()
    {
        var path = CachePath();
        using var cache = new ReplicantCache(path);
        var services = new ServiceCollection();
        services.AddSingleton(cache);
        services.AddHttpClient("CachedClient")
            .ConfigurePrimaryHttpMessageHandler(
                () => new MockHttpMessageHandler(
                    new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("not found")
                    }))
            .AddHttpMessageHandler(
                p => new ReplicantHandler(p.GetRequiredService<ReplicantCache>(), cache404: true));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("CachedClient");

        using var response = await client.GetAsync("http://example.com/cache404shared");
        AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        AreEqual("not found", content);

        var binFiles = Directory.GetFiles(path, "*.bin");
        AreEqual(1, binFiles.Length);
    }
}
