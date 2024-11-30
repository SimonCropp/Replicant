#if DEBUG
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework.Legacy;

[TestFixture]
public class HttpCacheTests
{
    HttpCache httpCache= new(
        CachePath,
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        });

    static string CachePath = Path.Combine(Path.GetTempPath(), "DownloadTests");

    [TearDown]
    public void Purge()
    {
        Directory.Delete(CachePath, true);
        Directory.CreateDirectory(CachePath);
    }

    [Test]
    public async Task EnsureExpiryIsCorrect()
    {
        var result = await httpCache.DownloadAsync("https://httpbin.org/json");
        var path = result.File!.Value.Content;
        var time = Timestamp.FromPath(path);
        ClassicAssert.Null(time.Expiry);
        ClassicAssert.AreEqual(FileEx.MinFileDate, File.GetLastWriteTimeUtc(path));
    }

    static async Task Construction(string cacheDirectory)
    {
        #region Construction

        var httpCache = new HttpCache(
            cacheDirectory,
            // omit for default new HttpClient()
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            },
            // omit for the default of 1000
            maxEntries: 10000);

        // Dispose when finished
        await httpCache.DisposeAsync();

        #endregion
    }

    [Test]
    public void DependencyInjection()
    {
        #region DependencyInjection

        var services = new ServiceCollection();
        services.AddSingleton(_ => new HttpCache(CachePath));

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        ClassicAssert.NotNull(httpCache);

        #endregion
    }

    [Test]
    public void DependencyInjectionWithHttpFactory()
    {
        #region DependencyInjectionWithHttpFactory

        ServiceCollection services = new();
        services.AddHttpClient();
        services.AddSingleton(
            _ =>
            {
                var clientFactory = _.GetRequiredService<IHttpClientFactory>();
                return new HttpCache(CachePath, () => clientFactory.CreateClient());
            });

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        ClassicAssert.NotNull(httpCache);

        #endregion
    }

    [Test]
    public async Task DefaultInstance()
    {
        HttpCache.Default.Purge();
        #region DefaultInstance

        var content = await HttpCache.Default.DownloadAsync("https://httpbin.org/status/200");

        #endregion

        await Verify(content);
    }

    [Test]
    public async Task EmptyContent()
    {
        var content = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        await Verify(content);
    }

    [Test]
    public async Task DuplicateSlowDownloads()
    {
        var task = httpCache.DownloadAsync("https://httpbin.org/delay/1");
        await Task.Delay(100);
        var result2 = await httpCache.DownloadAsync("https://httpbin.org/delay/1");

        var result1 = await task;

        ClassicAssert.True(result1.Stored || result2.Stored);
    }

#if DEBUG
    //TODO: debug these on mac
    [Test]
    public async Task PurgeOldWhenContentFileLocked()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        using (result.AsResponseMessage())
        {
            var filePair = result.File!.Value;
            filePair.PurgeItem();
            ClassicAssert.True(filePair.Exists());
        }
    }

    [Test]
    public async Task PurgeOldWhenMetaFileLocked()
    {
        var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");

        using (result.AsResponseMessage())
        {
            var filePair = result.File!.Value;
            filePair.PurgeItem();
            ClassicAssert.True(filePair.Exists());
        }
    }
#endif

    [Test]
    public async Task LockedContentFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);
        using (result.AsResponseMessage())
        {
            result = await httpCache.DownloadAsync(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verify(httpResponseMessage);
    }

    [Test]
    public async Task LockedMetaFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);
        using (new FileStream(result.File!.Value.Content, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await httpCache.DownloadAsync(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verify(httpResponseMessage);
    }

    [Test]
    public async Task FullHttpResponseMessageAsync()
    {
        #region FullHttpResponseMessage

        using var response = await httpCache.ResponseAsync("https://httpbin.org/status/200");

        #endregion

        await Verify(response);
    }

    [Test]
    public async Task FullHttpResponseMessage()
    {
        using var response = httpCache.Response("https://httpbin.org/status/200");
        await Verify(response);
    }

    [Test]
    public async Task NoCache()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/response-headers?Cache-Control=no-cache");
        await Verify(result);
    }

    [Test]
    public async Task NoStore()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/response-headers?Cache-Control=no-store");
        ClassicAssert.NotNull(result.Response);
        await Verify(result);
    }

    [Test]
    public async Task Etag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verify(content);
    }

    [Test]
    public async Task ExpiredShouldNotSendEtag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.DownloadAsync(uri);
        Recording.Start();
        var content = await httpCache.DownloadAsync(uri);
        await Verify(content);
    }

    [Test]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verify(content);
    }

    [Test]
    public async Task WithContent()
    {
        var content = await httpCache.DownloadAsync("https://httpbin.org/json");
        await Verify(content);
    }

    [Test]
    public Task WithContentSync()
    {
        var content = httpCache.Download("https://httpbin.org/json");
        return Verify(content);
    }

    [Test]
    public async Task String()
    {
        #region string

        var content = await httpCache.StringAsync("https://httpbin.org/json");

        #endregion

        await Verify(content);
    }

    [Test]
    public async Task Lines()
    {
        #region string

        var lines = new List<string>();
        await foreach (var line in httpCache.LinesAsync("https://httpbin.org/json"))
        {
            lines.Add(line);
        }

        #endregion

        await Verify(lines);
    }

    [Test]
    public async Task Bytes()
    {
        #region bytes

        var bytes = await httpCache.BytesAsync("https://httpbin.org/json");

        #endregion

        await Verify(Encoding.UTF8.GetString(bytes));
    }

    [Test]
    public async Task Stream()
    {
        #region stream

        using var stream = await httpCache.StreamAsync("https://httpbin.org/json");

        #endregion

        await Verify(stream, "txt");
    }

    [Test]
    public async Task ToFile()
    {
        var targetFile = FileEx.GetTempFileName("txt");
        try
        {
            #region ToFile

            await httpCache.ToFileAsync("https://httpbin.org/json", targetFile);

            #endregion

            await VerifyFile(targetFile);
        }
        finally
        {
            File.Delete(targetFile);
        }
    }

    [Test]
    public async Task ToStream()
    {
        var targetStream = new MemoryStream();

        #region ToStream

        await httpCache.ToStreamAsync("https://httpbin.org/json", targetStream);

        #endregion

        await Verify(targetStream, "txt");
    }

    [Test]
    public async Task ModifyRequest()
    {
        var uri = "https://httpbin.org/json";

        #region ModifyRequest

        var content = await httpCache.StringAsync(
            uri,
            modifyRequest: message =>
            {
                message.Headers.Add("Key1", "Value1");
                message.Headers.Add("Key2", "Value2");
            });

        #endregion

        await Verify(content);
    }

    [Test]
    public Task NotFound() =>
        ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/404"));

    [Test]
    public async Task Timeout()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(10)
        };
        var cache = new HttpCache(CachePath, httpClient);
        var uri = "https://httpbin.org/delay/1";
        await ThrowsTask(() => cache.StringAsync(uri))
            .UniqueForRuntime()
            .IgnoreMembers("InnerException", "CancellationToken");
    }

    [Test]
    public async Task TimeoutUseStale()
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        var httpCache = new HttpCache(CachePath, httpClient);
        var uri = "https://httpbin.org/delay/1";

        #region AddItem

        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("the content")
        };
        await httpCache.AddItemAsync(uri, response);

        #endregion

        await Verify(httpCache.DownloadAsync(uri, true));
    }

    [Test]
    public Task ServerError() =>
        ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/500"));

    [Test]
    public async Task ServerErrorDontUseStale()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
            {
                Headers =
                {
                    Expires = DateTimeOffset.Now.AddDays(-1)
                }
            },
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItemAsync(uri, response);
        await ThrowsTask(() => httpCache.StringAsync(uri));
    }

    [Test]
    public async Task ServerErrorUseStale()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("content")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItemAsync(uri, response);

        #region staleIfError

        var content = httpCache.StringAsync(uri, staleIfError: true);

        #endregion

        await Verify(content);
    }
}
#endif