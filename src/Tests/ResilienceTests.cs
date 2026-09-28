// ReSharper disable UnusedVariable
// ReSharper disable ShortLivedHttpClient

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

public class ResilienceTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantResilienceTests");

    static string CachePath([CallerMemberName] string name = "") =>
        Path.Combine(root, name);

    [After(Class)]
    public static void Cleanup()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    static void HttpClientFactoryWithResilienceUsage(string cacheDirectory)
    {
        #region HttpClientFactoryWithResilienceUsage

        var services = new ServiceCollection();
        services.AddReplicantCache(cacheDirectory);
        services.AddHttpClient("api", _ => _.BaseAddress = new("https://example.com"))
            // Register the cache FIRST so it sits OUTERMOST in the pipeline.
            // Cache hits short-circuit immediately and never consume retry,
            // timeout, or circuit-breaker budget.
            .AddReplicantCaching(staleIfError: true)
            // Resilience pipeline sits INSIDE the cache, so it only wraps
            // actual upstream calls (and conditional GETs during revalidation).
            .AddResilienceHandler(
                "api-pipeline",
                builder => builder
                    .AddRetry(
                        new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        Delay = TimeSpan.FromSeconds(1),
                        BackoffType = DelayBackoffType.Exponential,
                        UseJitter = true,
                    })
                    .AddCircuitBreaker(
                        new HttpCircuitBreakerStrategyOptions
                    {
                        SamplingDuration = TimeSpan.FromSeconds(10),
                        FailureRatio = 0.5,
                        MinimumThroughput = 10,
                        BreakDuration = TimeSpan.FromSeconds(30),
                    })
                    .AddTimeout(TimeSpan.FromSeconds(10)));

        #endregion
    }

    static async Task ManualResilienceUsage(string cacheDirectory, Cancel cancel)
    {
        #region ManualResilienceUsage

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(
                new()
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(_ => _.StatusCode >= HttpStatusCode.InternalServerError)
                        .HandleResult(_ => _.StatusCode == HttpStatusCode.TooManyRequests)
                })
            .AddCircuitBreaker(
                new()
                {
                    SamplingDuration = TimeSpan.FromSeconds(10),
                    FailureRatio = 0.5,
                    MinimumThroughput = 10,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(_ => _.StatusCode >= HttpStatusCode.InternalServerError)
                })
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();

        // Resilience handler wraps the primary HTTP handler...
        var resilienceHandler = new ResilienceHandler(pipeline)
        {
            InnerHandler = new SocketsHttpHandler()
        };

        // ...and the cache wraps the resilience handler. Cache hits never
        // touch the resilience pipeline.
        var cachingHandler = new ReplicantHandler(
            cacheDirectory,
            innerHandler: resilienceHandler,
            staleIfError: true);

        using var httpClient = new HttpClient(cachingHandler);

        var response = await httpClient.GetAsync(
            "https://example.com/api/data",
            cancel);
        response.EnsureSuccessStatusCode();

        #endregion
    }

    [Test]
    public async Task Manual_CacheHit_ShortCircuitsResiliencePipeline()
    {
        var path = CachePath();
        var mock = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("payload")
            });
        var counting = new CountingHandler(mock);

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(FastRetryOptions())
            .Build();

        using var resilienceHandler = new ResilienceHandler(pipeline)
        {
            InnerHandler = counting
        };
        using var handler = new ReplicantHandler(path, resilienceHandler);
        using var client = new HttpClient(handler);

        var first = await client.GetStringAsync("http://example.com/cached");
        await Assert.That(first).IsEqualTo("payload");
        await Assert.That(counting.Count).IsEqualTo(1);

        // Second request should be served from cache, never reaching the
        // resilience handler or the underlying mock.
        var second = await client.GetStringAsync("http://example.com/cached");
        await Assert.That(second).IsEqualTo("payload");
        await Assert.That(counting.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Manual_ResilienceRetries_BeforeCaching()
    {
        var path = CachePath();
        var mock = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("transient")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("eventual success")
            });
        var counting = new CountingHandler(mock);

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(FastRetryOptions())
            .Build();

        using var resilienceHandler = new ResilienceHandler(pipeline)
        {
            InnerHandler = counting
        };
        // maxRetries: 0 on Replicant — retries are owned by the resilience pipeline.
        using var handler = new ReplicantHandler(path, resilienceHandler, maxRetries: 0);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/retried");
        await Assert.That(content).IsEqualTo("eventual success");
        await Assert.That(counting.Count).IsEqualTo(2);

        var binFiles = Directory.GetFiles(path, "*.bin");
        await Assert.That(binFiles.Length).IsEqualTo(1);
    }

    [Test]
    public async Task HttpClientFactory_CacheOutermost_HitShortCircuitsResilience()
    {
        var path = CachePath();
        var mock = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("factory payload")
            });
        var counting = new CountingHandler(mock);

        var services = new ServiceCollection();
        services.AddReplicantCache(path);
        services.AddHttpClient("api")
            .ConfigurePrimaryHttpMessageHandler(() => counting)
            // Replicant registered first => outermost handler in the pipeline.
            .AddReplicantCaching()
            // Resilience registered second => sits between cache and primary handler.
            .AddResilienceHandler(
                "test-pipeline",
                builder => builder.AddRetry(FastRetryOptions()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("api");

        var first = await client.GetStringAsync("http://example.com/factory-cached");
        await Assert.That(first).IsEqualTo("factory payload");
        await Assert.That(counting.Count).IsEqualTo(1);

        // Second request: cache hit. Should not reach the resilience handler.
        var second = await client.GetStringAsync("http://example.com/factory-cached");
        await Assert.That(second).IsEqualTo("factory payload");
        await Assert.That(counting.Count).IsEqualTo(1);
    }

    [Test]
    public async Task HttpClientFactory_ResilienceRetries_ThenCaches()
    {
        var path = CachePath();
        var mock = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("bad gateway")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok after retry")
            });
        var counting = new CountingHandler(mock);

        var services = new ServiceCollection();
        services.AddReplicantCache(path);
        services.AddHttpClient("api")
            .ConfigurePrimaryHttpMessageHandler(() => counting)
            .AddReplicantCaching()
            .AddResilienceHandler(
                "test-pipeline",
                builder => builder.AddRetry(FastRetryOptions()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("api");

        var content = await client.GetStringAsync("http://example.com/factory-retry");
        await Assert.That(content).IsEqualTo("ok after retry");
        await Assert.That(counting.Count).IsEqualTo(2);

        var binFiles = Directory.GetFiles(path, "*.bin");
        await Assert.That(binFiles.Length).IsEqualTo(1);
    }

    [Test]
    public async Task HttpClientFactory_NonGet_BypassesCacheButUsesResilience()
    {
        var path = CachePath();
        var mock = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server error")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("posted")
            });
        var counting = new CountingHandler(mock);

        var services = new ServiceCollection();
        services.AddReplicantCache(path);
        services.AddHttpClient("api")
            .ConfigurePrimaryHttpMessageHandler(() => counting)
            .AddReplicantCaching()
            .AddResilienceHandler(
                "test-pipeline",
                builder => builder.AddRetry(FastRetryOptions()));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("api");

        using var response = await client.PostAsync(
            "http://example.com/factory-post",
            new StringContent("body"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);

        // POST bypasses Replicant's cache logic, but still flows through the
        // resilience pipeline — first call returned 500, retry returned 200.
        await Assert.That(counting.Count).IsEqualTo(2);

        var binFiles = Directory.GetFiles(path, "*.bin");
        await Assert.That(binFiles.Length).IsEqualTo(0);
    }

    static HttpRetryStrategyOptions FastRetryOptions() =>
        new()
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.Zero,
            BackoffType = DelayBackoffType.Constant,
            UseJitter = false,
        };

    class CountingHandler(HttpMessageHandler inner) :
        DelegatingHandler(inner)
    {
        int count;

        public int Count => count;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            Cancel cancel)
        {
            Interlocked.Increment(ref count);
            return base.SendAsync(request, cancel);
        }
    }
}
