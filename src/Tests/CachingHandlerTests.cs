#if DEBUG
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class CachingHandlerTests
{
    #region Basic Functionality Tests

    [Test]
    public async Task FirstRequestCachesToDisk()
    {
        using var temp = new TempDirectory();

        string content;
        string cacheStatus;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);
            using var response = await client.GetAsync("https://httpbin.org/json");

            content = await response.Content.ReadAsStringAsync();
            cacheStatus = response.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
        }

        IsTrue(content.Length > 0);
        IsTrue(cacheStatus is "miss" or "revalidate", $"Expected miss or revalidate but got {cacheStatus}");

        // Verify files exist on disk
        var files = Directory.GetFiles(temp, "*.bin", SearchOption.AllDirectories);
        AreEqual(1, files.Length);
    }

    [Test]
    public async Task SecondRequestReturnsFromCache()
    {
        using var temp = new TempDirectory();

        string content;
        string cacheStatus;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);

            // First request
            using (var response1 = await client.GetAsync("https://httpbin.org/json"))
            {
                await response1.Content.ReadAsStringAsync();
            }

            // Second request should come from cache
            using var response2 = await client.GetAsync("https://httpbin.org/json");
            content = await response2.Content.ReadAsStringAsync();
            cacheStatus = response2.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
        }

        IsTrue(content.Length > 0);
        AreEqual("hit", cacheStatus);
    }

    [Test]
    public async Task CacheDirectoryStructureValidation()
    {
        using var temp = new TempDirectory();

        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);
            using var response = await client.GetAsync("https://httpbin.org/json");

            await response.Content.ReadAsStringAsync();
        }

        // Verify both .bin and .json files exist
        var binFiles = Directory.GetFiles(temp, "*.bin", SearchOption.AllDirectories);
        var jsonFiles = Directory.GetFiles(temp, "*.json", SearchOption.AllDirectories);

        AreEqual(1, binFiles.Length);
        AreEqual(1, jsonFiles.Length);
    }

    #endregion

    #region HTTP Method Tests

    [Test]
    public async Task PostBypassesCache()
    {
        using var temp = new TempDirectory();

        bool hasHeader1;
        bool hasHeader2;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);

            // First POST
            using (var response1 = await client.PostAsync("https://httpbin.org/post", new StringContent("data")))
            {
                hasHeader1 = response1.Headers.Contains(CachingHandler.CacheStatusHeaderName);
            }

            // Second POST
            using (var response2 = await client.PostAsync("https://httpbin.org/post", new StringContent("data")))
            {
                hasHeader2 = response2.Headers.Contains(CachingHandler.CacheStatusHeaderName);
            }
        }

        False(hasHeader1);
        False(hasHeader2);
    }

    [Test]
    public async Task HeadCachesLikeGet()
    {
        using var temp = new TempDirectory();

        string status1;
        string status2;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);

            // First HEAD request
            var request1 = new HttpRequestMessage(HttpMethod.Head, "https://httpbin.org/json");
            using (var response1 = await client.SendAsync(request1))
            {
                status1 = response1.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
            }

            // Second HEAD request should come from cache
            var request2 = new HttpRequestMessage(HttpMethod.Head, "https://httpbin.org/json");
            using (var response2 = await client.SendAsync(request2))
            {
                status2 = response2.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
            }
        }

        IsTrue(status1 is "miss" or "revalidate");
        AreEqual("hit", status2);
    }

    #endregion

    #region Cache Status Header Tests

    [Test]
    public async Task HitScenarioSetsCorrectHeader()
    {
        using var temp = new TempDirectory();

        string cacheStatus;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);

            using (await client.GetAsync("https://httpbin.org/json"))
            {
            }

            using var response = await client.GetAsync("https://httpbin.org/json");
            cacheStatus = response.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
        }

        AreEqual("hit", cacheStatus);
    }

    [Test]
    public async Task MissScenarioSetsCorrectHeader()
    {
        using var temp = new TempDirectory();

        string cacheStatus;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);
            using var response = await client.GetAsync("https://httpbin.org/json");

            cacheStatus = response.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
        }

        IsTrue(cacheStatus is "miss" or "revalidate");
    }

    [Test]
    public async Task HeaderNameConstantWorks()
    {
        using var temp = new TempDirectory();

        bool hasHeader;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);
            using var response = await client.GetAsync("https://httpbin.org/json");

            hasHeader = response.Headers.Contains(CachingHandler.CacheStatusHeaderName);
        }

        IsTrue(hasHeader);
        AreEqual("X-Replicant-Cache-Status", CachingHandler.CacheStatusHeaderName);
    }

    #endregion

    #region Integration Tests

    [Test]
    public async Task WorksWithHttpClientFactory()
    {
        #region DelegatingHandlerWithFactory

        var services = new ServiceCollection();
        services.AddHttpClient("cached")
            .ConfigurePrimaryHttpMessageHandler(() => new CachingHandler());

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("cached");

        #endregion

        NotNull(client);
    }

    [Test]
    public async Task WorksWithDependencyInjection()
    {
        using var temp = new TempDirectory();
        var services = new ServiceCollection();
        services.AddSingleton<CachingHandler>(_ => new CachingHandler(temp));
        services.AddHttpClient("cached")
            .ConfigurePrimaryHttpMessageHandler(sp => sp.GetRequiredService<CachingHandler>());

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("cached");

        NotNull(client);
    }

    #endregion

    #region Disposal and Cleanup Tests

    [Test]
    public void DisposeCleansUpResources()
    {
        using var temp = new TempDirectory();
        var handler = new CachingHandler(temp);
        handler.Dispose();

        // Should not throw
        Assert.DoesNotThrow(() => handler.Dispose());
    }

    [Test]
    public async Task PurgeClearsCache()
    {
        using var temp = new TempDirectory();

        string status1;
        string status2;
        {
            using var handler = new CachingHandler(temp);
            using var client = new HttpClient(handler);

            // Cache a response
            using (var response1 = await client.GetAsync("https://httpbin.org/json"))
            {
                status1 = response1.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
            }

            // Purge cache
            handler.Purge();

            // Next request should miss
            using (var response2 = await client.GetAsync("https://httpbin.org/json"))
            {
                status2 = response2.Headers.GetValues(CachingHandler.CacheStatusHeaderName).First();
            }
        }

        IsTrue(status1 is "miss" or "revalidate");
        IsTrue(status2 is "miss" or "revalidate");
    }

    #endregion

    #region Documentation Snippets

    [Test]
    public async Task DelegatingHandlerBasic()
    {
        #region DelegatingHandlerBasic

        var handler = new CachingHandler();
        var httpClient = new HttpClient(handler);
        var response = await httpClient.GetAsync("https://httpbin.org/json");

        #endregion

        NotNull(response);
    }

    [Test]
    public async Task DelegatingHandlerStaleIfError()
    {
        #region DelegatingHandlerStaleIfError

        var handler = new CachingHandler { StaleIfError = true };

        #endregion

        NotNull(handler);
    }

    [Test]
    public async Task DelegatingHandlerCacheStatus()
    {
        using var temp = new TempDirectory();

        string? status;
        {
            using var handler = new CachingHandler(temp);
            using var httpClient = new HttpClient(handler);
            using var response = await httpClient.GetAsync("https://httpbin.org/json");

            #region DelegatingHandlerCacheStatus

            status = response.Headers
                .GetValues(CachingHandler.CacheStatusHeaderName)
                .First();

            #endregion
        }

        NotNull(status);
    }

    #endregion
}
#endif
