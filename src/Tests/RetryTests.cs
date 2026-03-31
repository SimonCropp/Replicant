[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class RetryTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantRetryTests");

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

    static async Task RetryUsage(string cacheDirectory)
    {
        #region RetryUsage

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
        AreEqual("success", content);
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
        AreEqual("success", content);
    }

    [Test]
    public void RetriesExhausted_Throws()
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

        Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStringAsync("http://example.com/retry-exhausted"));
    }

    [Test]
    public void NonRetryableStatus_NotRetried()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("not found")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStringAsync("http://example.com/retry-404"));
    }

    [Test]
    public void RetryDisabled_Throws()
    {
        var path = CachePath();
        var inner = new MockHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("error")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStringAsync("http://example.com/no-retry"));
    }

    [Test]
    public void Exception_ThenSuccess_ReturnsContent()
    {
        var path = CachePath();
        var inner = new ThrowThenSucceedHandler(
            timesToThrow: 1,
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("recovered")
            });
        using var handler = new ReplicantHandler(path, inner);
        using var client = new HttpClient(handler);

        // Without retry, the exception propagates
        Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetStringAsync("http://example.com/throw-no-retry"));
    }

    [Test]
    public async Task Exception_WithRetry_ThenSuccess()
    {
        var path = CachePath();
        var inner = new ThrowThenSucceedHandler(
            timesToThrow: 1,
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("recovered")
            });
        using var handler = new ReplicantHandler(path, inner, maxRetries: 3);
        using var client = new HttpClient(handler);

        var content = await client.GetStringAsync("http://example.com/throw-retry");
        AreEqual("recovered", content);
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
        AreEqual("original", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: revalidation retries past 503, gets 200
        var content2 = await client.GetStringAsync("http://example.com/revalidate-retry");
        AreEqual("updated", content2);
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
        AreEqual("cached content", content1);

        // Expire the cached file
        var binFile = Directory.GetFiles(path, "*.bin").Single();
        File.SetLastWriteTimeUtc(binFile, new(2020, 1, 1));

        // Second request: retries exhausted, staleIfError returns cached content
        var content2 = await client.GetStringAsync("http://example.com/retry-stale");
        AreEqual("cached content", content2);
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
            AreEqual($"success after {status}", content);
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
