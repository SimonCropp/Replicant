// ReSharper disable ShortLivedHttpClient

[TestFixture]
[Parallelizable(ParallelScope.Children)]
public class RevalidationTests
{
    static string root = Path.Combine(Path.GetTempPath(), "ReplicantRevalidationTests");

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

    [Test]
    public async Task ChangedEtag_ReplacesSupersededEntry()
    {
        var path = CachePath();
        var server = new FakeServer("W/\"l1\"", "content1")
        {
            CacheControl = new()
            {
                MaxAge = TimeSpan.Zero,
                Private = true,
                MustRevalidate = true
            }
        };
        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);
        var uri = "http://example.com/changed-etag";

        AreEqual("content1", await client.GetStringAsync(uri));
        AreEqual("content1", await client.GetStringAsync(uri));

        server.Etag = "W/\"l2\"";
        server.Content = "content2";

        AreEqual("content2", await client.GetStringAsync(uri));
        AreEqual("content2", await client.GetStringAsync(uri));
        AreEqual("content2", await client.GetStringAsync(uri));

        CollectionAssert.AreEqual(
            new[] {null, "W/\"l1\"", "W/\"l1\"", "W/\"l2\"", "W/\"l2\""},
            server.SentIfNoneMatch);
        CollectionAssert.AreEqual(
            new[] {HttpStatusCode.OK, HttpStatusCode.NotModified, HttpStatusCode.OK, HttpStatusCode.NotModified, HttpStatusCode.NotModified},
            server.Statuses);
        AreEqual(1, Directory.GetFiles(path, "*.bin").Length);
        AreEqual(1, Directory.GetFiles(path, "*.json").Length);
    }

    [Test]
    public async Task EtagWithoutExpiry_Revalidates()
    {
        var path = CachePath();
        var server = new FakeServer("\"v1\"", "one");
        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);
        var uri = "http://example.com/etag-no-expiry";

        AreEqual("one", await client.GetStringAsync(uri));
        AreEqual("one", await client.GetStringAsync(uri));

        server.Etag = "\"v2\"";
        server.Content = "two";

        AreEqual("two", await client.GetStringAsync(uri));

        CollectionAssert.AreEqual(
            new[] {null, "\"v1\"", "\"v1\""},
            server.SentIfNoneMatch);
    }

    [Test]
    public async Task NoCache_RevalidatesEveryUse()
    {
        var path = CachePath();
        var server = new FakeServer("\"v1\"", "one")
        {
            CacheControl = new()
            {
                NoCache = true
            }
        };
        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);
        var uri = "http://example.com/no-cache";

        AreEqual("one", await client.GetStringAsync(uri));
        // 304 carries no-cache too, and must not replace the cached content
        AreEqual("one", await client.GetStringAsync(uri));
        AreEqual("one", await client.GetStringAsync(uri));

        server.Etag = "\"v2\"";
        server.Content = "two";

        AreEqual("two", await client.GetStringAsync(uri));

        CollectionAssert.AreEqual(
            new[] {null, "\"v1\"", "\"v1\"", "\"v1\""},
            server.SentIfNoneMatch);
    }

    [Test]
    public void InvalidExpires_IsParsedAsMinValue()
    {
        foreach (var value in new[] {"-1", "0"})
        {
            using var content = new StringContent("");
            content.Headers.TryAddWithoutValidation("Expires", value);
            AreEqual(DateTimeOffset.MinValue, content.Headers.Expires);
        }
    }

    [TestCase("-1", false)]
    [TestCase("0", false)]
    [TestCase("-1", true)]
    public async Task InvalidExpires_Revalidates(string expires, bool noCache)
    {
        var path = CachePath($"InvalidExpires_{expires}_{noCache}");
        var server = new FakeServer("\"v1\"", "one")
        {
            Expires = expires
        };
        if (noCache)
        {
            server.CacheControl = new()
            {
                NoCache = true
            };
        }

        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);
        var uri = "http://example.com/invalid-expires";

        AreEqual("one", await client.GetStringAsync(uri));

        server.Etag = "\"v2\"";
        server.Content = "two";

        AreEqual("two", await client.GetStringAsync(uri));

        CollectionAssert.AreEqual(
            new[] {null, "\"v1\""},
            server.SentIfNoneMatch);
    }

    [Test]
    public async Task NoExpiry_WithLastModified_UsesHeuristicFreshness()
    {
        var path = CachePath();
        var server = new FakeServer(null, "one")
        {
            LastModified = DateTimeOffset.UtcNow.AddDays(-5)
        };
        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);
        var uri = "http://example.com/heuristic";

        AreEqual("one", await client.GetStringAsync(uri));
        server.Content = "two";
        // 10% of 5 days = 12 hours, so still fresh
        AreEqual("one", await client.GetStringAsync(uri));

        AreEqual(1, server.SentIfNoneMatch.Count);

        var expiry = Timestamp.FromPath(Directory.GetFiles(path, "*.bin").Single()).Expiry!.Value;
        var expected = DateTimeOffset.UtcNow.AddHours(12);
        True(Math.Abs((expiry - expected).TotalMinutes) < 1, $"expiry {expiry:O} expected about {expected:O}");
    }

    [Test]
    public async Task NoExpiry_WithOldLastModified_HeuristicIsCapped()
    {
        var path = CachePath();
        var server = new FakeServer(null, "one")
        {
            LastModified = DateTimeOffset.UtcNow.AddYears(-10)
        };
        using var handler = new ReplicantHandler(path, server);
        using var client = new HttpClient(handler);

        await client.GetStringAsync("http://example.com/heuristic-cap");

        var expiry = Timestamp.FromPath(Directory.GetFiles(path, "*.bin").Single()).Expiry!.Value;
        True(expiry <= DateTimeOffset.UtcNow.AddDays(1).AddMinutes(1), $"expiry {expiry:O}");
    }

    [Test]
    public async Task AlwaysRevalidate_IgnoresMaxAgeAndMinFreshness()
    {
        var cacheDirectory = CachePath();
        var server = new FakeServer("\"v1\"", "one")
        {
            CacheControl = new()
            {
                MaxAge = TimeSpan.FromDays(1)
            }
        };

        #region AlwaysRevalidate

        using var handler = new ReplicantHandler(
            cacheDirectory,
            server,
            alwaysRevalidate: true);

        #endregion

        using var client = new HttpClient(handler);
        var uri = "http://example.com/always-revalidate";

        AreEqual("one", await client.GetStringAsync(uri));
        AreEqual("one", await client.GetStringAsync(uri));

        server.Etag = "\"v2\"";
        server.Content = "two";

        AreEqual("two", await client.GetStringAsync(uri));

        CollectionAssert.AreEqual(
            new[] {null, "\"v1\"", "\"v1\""},
            server.SentIfNoneMatch);
    }

    [Test]
    public async Task AlwaysRevalidate_HttpCache()
    {
        var path = CachePath();
        var mock = new MockHttpClient(
            new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                Content = new StringContent("")
            });
        await using var cache = new HttpCache(path, mock, alwaysRevalidate: true);
        await cache.AddItemAsync("http://test/always", "content", expiry: DateTimeOffset.UtcNow.AddDays(1), etag: "\"e\"");

        using var result = await cache.DownloadAsync("http://test/always");

        True(result.Revalidated);
        AreEqual("content", await result.AsStringAsync(default));
        AreEqual(1, mock.Requests.Count);
    }

    class FakeServer(string? etag, string content) :
        HttpMessageHandler
    {
        public string? Etag { get; set; } = etag;
        public string Content { get; set; } = content;
        public CacheControlHeaderValue? CacheControl { get; set; }
        public string? Expires { get; set; }
        public DateTimeOffset? LastModified { get; set; }
        public List<string?> SentIfNoneMatch { get; } = [];
        public List<HttpStatusCode> Statuses { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Cancel cancel)
        {
            string? ifNoneMatch = null;
            if (request.Headers.TryGetValues("If-None-Match", out var values))
            {
                ifNoneMatch = string.Join(",", values);
            }

            SentIfNoneMatch.Add(ifNoneMatch);

            var notModified = Etag != null && ifNoneMatch == Etag;
            var response = notModified
                ? new HttpResponseMessage(HttpStatusCode.NotModified)
                {
                    Content = new StringContent("")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(Content)
                };
            Statuses.Add(response.StatusCode);

            response.Headers.CacheControl = CacheControl;
            if (Etag != null)
            {
                response.Headers.TryAddWithoutValidation("ETag", Etag);
            }

            if (Expires != null)
            {
                response.Content.Headers.TryAddWithoutValidation("Expires", Expires);
            }

            response.Content.Headers.LastModified = LastModified;
            return Task.FromResult(response);
        }
    }
}
