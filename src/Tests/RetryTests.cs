// ReSharper disable UnusedVariable
// ReSharper disable ShortLivedHttpClient
public class RetryTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantRetryTests");

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

    static async Task RetryHttpCacheUsage(string cacheDirectory)
    {
        #region RetryHttpCacheUsage

        using var httpCache = new HttpCache(cacheDirectory, maxRetries: 3);
        var content = await httpCache.StringAsync("https://example.com");

        #endregion
    }

    static async Task RetryHandlerUsage(string cacheDirectory)
    {
        #region RetryHandlerUsage

        var handler = new ReplicantHandler(cacheDirectory, maxRetries: 3)
        {
            InnerHandler = new HttpClientHandler()
        };
        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://example.com");

        #endregion
    }

    [Test]
    public async Task ServerError_ThenSuccess_ReturnsContent()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("success")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/retry");
        await Assert.That(content).IsEqualTo("success");
    }

    [Test]
    public async Task MultipleRetries_ThenSuccess()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("error1")
            },
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
            {
                Content = new StringContent("error2")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("success")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/retry-multi");
        await Assert.That(content).IsEqualTo("success");
    }

    [Test]
    public async Task RetriesExhausted_Throws()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error1")
            },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error2")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 1);
        using var client = new HttpClient(handler);

        await Assert.That(() => (Task) client.GetStringAsync("http://example.com/retry-exhausted")).ThrowsExactly<HttpRequestException>();
    }

    [Test]
    public async Task NonRetryableStatus_NotRetried()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        await Assert.That(() => (Task) client.GetStringAsync("http://example.com/retry-404")).ThrowsExactly<HttpRequestException>();
    }

    [Test]
    public async Task RetryDisabled_Throws()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        await Assert.That(() => (Task) client.GetStringAsync("http://example.com/no-retry")).ThrowsExactly<HttpRequestException>();
    }

    [Test]
    public async Task Exception_ThenSuccess_ReturnsContent()
    {
        var path = CachePath();
        var inner = new ThrowThenSucceedHandler(
            timesToThrow: 1,
            new(HttpStatusCode.OK)
            {
                Content = new StringContent("recovered")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        // Without retry, the exception propagates
        await Assert.That(() => (Task) client.GetStringAsync("http://example.com/throw-no-retry")).ThrowsExactly<HttpRequestException>();
    }

    [Test]
    public async Task Exception_WithRetry_ThenSuccess()
    {
        var path = CachePath();
        var inner = new ThrowThenSucceedHandler(
            timesToThrow: 1,
            new(HttpStatusCode.OK)
            {
                Content = new StringContent("recovered")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/throw-retry");
        await Assert.That(content).IsEqualTo("recovered");
    }

    [Test]
    public async Task Revalidation_ServerError_ThenSuccess()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("original")
            },
            // Revalidation: first attempt fails, second succeeds
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("updated")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/revalidate-retry");
        await Assert.That(content1).IsEqualTo("original");

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: revalidation retries past 503, gets 200
        var content2 = await client.GetStringAsync("http://example.com/revalidate-retry");
        await Assert.That(content2).IsEqualTo("updated");
    }

    [Test]
    public async Task RetryWithStaleIfError_RetriesFirst_ThenFallsBackToStale()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("cached content"),
            },
            // Revalidation: all retries fail
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error1")
            },
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error2")
            });
        using var handler = new ReplicantHandler(path, inner, staleIfError: true, maxRetries: 1);
        using var client = new HttpClient(handler);

        // First request: stored
        var content1 = await client.GetStringAsync("http://example.com/retry-stale");
        await Assert.That(content1).IsEqualTo("cached content");

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: retries exhausted, staleIfError returns cached content
        var content2 = await client.GetStringAsync("http://example.com/retry-stale");
        await Assert.That(content2).IsEqualTo("cached content");
    }

    [Test]
    public async Task AllRetryableStatusCodes_AreRetried()
    {
        HttpStatusCode[] retryableStatuses =
        [
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        ];

        foreach (var status in retryableStatuses)
        {
            var path = Path.Combine(root, $"AllRetryable_{status}");
            var inner = new MockHttpMessageHandler(
                new HttpResponseMessage(status)
                {
                    Content = new StringContent("error")
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"success after {status}")
                });
            using var handler = new ReplicantHandler(path, inner, maxRetries: 1);
            using var client = new HttpClient(handler);

            var content = await client.GetStringAsync($"http://example.com/retry-{status}");
            await Assert.That(content).IsEqualTo($"success after {status}");
        }
    }

    class ThrowThenSucceedHandler(int timesToThrow, HttpResponseMessage success) :
        HttpMessageHandler
    {
        int callCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, Cancel cancel)
        {
            if (callCount++ < timesToThrow)
            {
                throw new HttpRequestException("Simulated transient failure");
            }

            return Task.FromResult(success);
        }
    }
}
