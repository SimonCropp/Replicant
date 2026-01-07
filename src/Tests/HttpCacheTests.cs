#if DEBUG
using Microsoft.Extensions.DependencyInjection;

[TestFixture]
public class HttpCacheTests
{
    static string cachePath = Path.Combine(Path.GetTempPath(), "DownloadTests");

    HttpCache httpCache= new(
        cachePath,
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        });

    [TearDown]
    public void Purge()
    {
        Directory.Delete(cachePath, true);
        Directory.CreateDirectory(cachePath);
    }

    [Test]
    public async Task EnsureExpiryIsCorrect()
    {
        var result = await httpCache.DownloadAsync("https://httpbin.org/json");
        var path = result.File!.Value.Content;
        var time = Timestamp.FromPath(path);
        Null(time.Expiry);
        AreEqual(FileEx.MinFileDate, File.GetLastWriteTimeUtc(path));
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
        services.AddSingleton(_ => new HttpCache(cachePath));

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        NotNull(httpCache);

        #endregion
    }

    [Test]
    public void DependencyInjectionWithHttpFactory()
    {
        #region DependencyInjectionWithHttpFactory

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton(
            _ =>
            {
                var clientFactory = _.GetRequiredService<IHttpClientFactory>();
                return new HttpCache(cachePath, () => clientFactory.CreateClient());
            });

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        NotNull(httpCache);

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

        True(result1.Stored || result2.Stored);
    }

#if DEBUG
    //TODO: debug these on mac
    [Test]
    public async Task PurgeOldWhenContentFileLocked()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        using (await result.AsResponseMessageAsync())
        {
            var filePair = result.File!.Value;
            filePair.PurgeItem();
            True(filePair.Exists());
        }
    }

    [Test]
    public async Task PurgeOldWhenMetaFileLocked()
    {
        var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");

        using (await result.AsResponseMessageAsync())
        {
            var filePair = result.File!.Value;
            filePair.PurgeItem();
            True(filePair.Exists());
        }
    }
#endif

    [Test]
    public async Task LockedContentFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);
        using (await result.AsResponseMessageAsync())
        {
            result = await httpCache.DownloadAsync(uri);
        }

        using var httpResponseMessage = await result.AsResponseMessageAsync();
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

        using var httpResponseMessage = await result.AsResponseMessageAsync();
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
        NotNull(result.Response);
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
    public async Task ExpiredShouldNotSendEtagOrIfModifiedSince()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);

        File.SetLastWriteTimeUtc(result.File!.Value.Content, new(2020,10,1));
        Recording.Start();
        result = await httpCache.DownloadAsync(uri);
        await Verify(result)
            .IgnoreMembers("traceparent", "Traceparent");
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
        var cache = new HttpCache(cachePath, httpClient);
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
        var httpCache = new HttpCache(cachePath, httpClient);
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
    public async Task SyncDownload_WithNullExpiry_ShouldUseCachedFile()
    {
        // Add an item without expiry headers - this will result in null Expiry
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test content")
        };
        // Use a real URL that we'll cache, then verify sync uses cache without network
        var uri = "https://httpbin.org/json";
        await httpCache.AddItemAsync(uri, response);

        // Bug: HttpCache.cs does "if (timestamp.Expiry > now)" but should be
        // "if (expiry == null || expiry > now)" like the async version.
        // When Expiry is null, sync version incorrectly tries to re-fetch from network
        // instead of returning the cached file.
        // ReSharper disable once MethodHasAsyncOverload
        var result = httpCache.Download(uri);

        // If the bug exists, this will have fetched new content from httpbin.org/json
        // If fixed, it should return our cached "test content"
        // ReSharper disable once UseAwaitUsing
        using var stream = result.AsStream();
        using var reader = new StreamReader(stream);
        // ReSharper disable once MethodHasAsyncOverload
        var content = reader.ReadToEnd();
        AreEqual("test content", content);
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

    [Test]
    public async Task Purge_ShouldOnlyProcessBinFiles()
    {
        // Add a cached item (creates both .bin and .json files)
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test content")
        };
        var uri = "https://httpbin.org/purge-test";
        await httpCache.AddItemAsync(uri, response);

        // Verify both files exist before purge
        var files = Directory.GetFiles(cachePath);
        var binFiles = files.Where(f => f.EndsWith(".bin")).ToList();
        var jsonFiles = files.Where(f => f.EndsWith(".json")).ToList();
        AreEqual(1, binFiles.Count);
        AreEqual(1, jsonFiles.Count);

        // Bug: Purge() iterates over ALL files including .json files.
        // When FilePair.FromContentFile() is called on a .json file,
        // it creates a pair where Content=Meta (both .json), causing
        // PurgeItem() to fail when trying to move the same file twice.
        httpCache.Purge();

        // After purge, all cache files should be deleted
        var remainingFiles = Directory.GetFiles(cachePath);
        AreEqual(0, remainingFiles.Length);
    }
}
#endif