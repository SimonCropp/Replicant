#if DEBUG
using Microsoft.Extensions.DependencyInjection;
// ReSharper disable AccessToDisposedClosure

[TestFixture]
public class HttpCacheTests
{
    [Test]
    public async Task EnsureExpiryIsCorrect()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var result = await httpCache.DownloadAsync("https://httpbin.org/json");
        var path = result.File!.Value.Content;
        var time = Timestamp.FromPath(path);
        Null(time.Expiry);
        AreEqual(FileEx.MinFileDate, File.GetLastWriteTimeUtc(path));
    }

    [Test]
    public static async Task Construction()
    {
        using var cacheDirectory = new TempDirectory();

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
        using var cacheDirectory = new TempDirectory();

        #region DependencyInjection

        var services = new ServiceCollection();
        services.AddSingleton(_ => new HttpCache(cacheDirectory));

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        NotNull(httpCache);

        #endregion
    }

    [Test]
    public void DependencyInjectionWithHttpFactory()
    {
        using var cacheDirectory = new TempDirectory();

        #region DependencyInjectionWithHttpFactory

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton(_ =>
        {
            var clientFactory = _.GetRequiredService<IHttpClientFactory>();
            return new HttpCache(cacheDirectory, clientFactory.CreateClient);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var content = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        await Verify(content);
    }

    [Test]
    public async Task DuplicateSlowDownloads()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);

        #region FullHttpResponseMessage

        using var response = await httpCache.ResponseAsync("https://httpbin.org/status/200");

        #endregion

        await Verify(response);
    }

    [Test]
    public async Task FullHttpResponseMessage()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        using var response = httpCache.Response("https://httpbin.org/status/200");
        await Verify(response);
    }

    [Test]
    public async Task NoCache()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        using var result = await httpCache.DownloadAsync("https://httpbin.org/response-headers?Cache-Control=no-cache");
        await Verify(result);
    }

    [Test]
    public async Task NoStore()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        using var result = await httpCache.DownloadAsync("https://httpbin.org/response-headers?Cache-Control=no-store");
        NotNull(result.Response);
        await Verify(result);
    }

    [Test]
    public async Task Etag()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verify(content);
    }

    [Test]
    public async Task ExpiredShouldNotSendEtagOrIfModifiedSince()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);

        File.SetLastWriteTimeUtc(result.File!.Value.Content, new(2020, 10, 1));
        Recording.Start();
        result = await httpCache.DownloadAsync(uri);
        await Verify(result)
            .IgnoreMembers("traceparent", "Traceparent");
    }

    [Test]
    public async Task CacheControlMaxAge()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var uri = "https://httpbin.org/cache/20";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verify(content);
    }

    [Test]
    public async Task WithContent()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var content = await httpCache.DownloadAsync("https://httpbin.org/json");
        await Verify(content);
    }

    [Test]
    public Task WithContentSync()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var content = httpCache.Download("https://httpbin.org/json");
        return Verify(content);
    }

    [Test]
    public async Task String()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);

        #region string

        var content = await httpCache.StringAsync("https://httpbin.org/json");

        #endregion

        await Verify(content);
    }

    [Test]
    public async Task Lines()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);

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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);

        #region bytes

        var bytes = await httpCache.BytesAsync("https://httpbin.org/json");

        #endregion

        await Verify(Encoding.UTF8.GetString(bytes));
    }

    [Test]
    public async Task Stream()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);

        #region stream

        using var stream = await httpCache.StreamAsync("https://httpbin.org/json");

        #endregion

        await Verify(stream, "txt");
    }

    [Test]
    public async Task ToFile()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        var targetStream = new MemoryStream();

        #region ToStream

        await httpCache.ToStreamAsync("https://httpbin.org/json", targetStream);

        #endregion

        await Verify(targetStream, "txt");
    }

    [Test]
    public async Task ModifyRequest()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
    public async Task NotFound()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        await ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/404"));
    }

    [Test]
    public async Task Timeout()
    {
        using var cacheDirectory = new TempDirectory();
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(10)
        };
        await using var cache = new HttpCache(cacheDirectory, httpClient);
        var uri = "https://httpbin.org/delay/1";
        await ThrowsTask(() => cache.StringAsync(uri))
            .UniqueForRuntime()
            .IgnoreMembers("InnerException", "CancellationToken");
    }

    [Test]
    public async Task TimeoutUseStale()
    {
        using var cacheDirectory = new TempDirectory();
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        var httpCache = new HttpCache(cacheDirectory, httpClient);
        var uri = "https://httpbin.org/delay/1";

        #region AddItem

        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("the content")
        };
        await httpCache.AddItemAsync(uri, response);

        #endregion

        await Verify(httpCache.DownloadAsync(uri, true));
        await httpCache.DisposeAsync();
    }

    [Test]
    public async Task SyncDownload_WithNullExpiry_ShouldUseCachedFile()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
    public async Task ServerError()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        await ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/500"));
    }

    [Test]
    public async Task ServerErrorDontUseStale()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
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
    public async Task Cache404()
    {
        using var cacheDirectory = new TempDirectory();
        var uri = "https://httpbin.org/status/404";

        #region cache404

        await using var cache = new HttpCache(cacheDirectory, cache404: true);
        var content = await cache.StringAsync(uri);

        #endregion

        await Verify(content);
    }

    [Test]
    public async Task MinFreshness()
    {
        using var cacheDirectory = new TempDirectory();

        #region MinFreshness

        await using var cache = new HttpCache(
            cacheDirectory,
            minFreshness: TimeSpan.FromHours(1));
        var content = await cache.StringAsync("https://httpbin.org/json");

        #endregion

        await Verify(content);
    }

    [Test]
    public async Task Purge_ShouldOnlyProcessBinFiles()
    {
        using var cacheDirectory = new TempDirectory();
        var httpCache = new HttpCache(cacheDirectory);
        // Add a cached item (creates both .bin and .json files)
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("test content")
        };
        var uri = "https://httpbin.org/purge-test";
        await httpCache.AddItemAsync(uri, response);

        // Verify both files exist before purge
        var files = Directory.GetFiles(cacheDirectory);
        var binFiles = files.Where(_ => _.EndsWith(".bin")).ToList();
        var jsonFiles = files.Where(_ => _.EndsWith(".json")).ToList();
        AreEqual(1, binFiles.Count);
        AreEqual(1, jsonFiles.Count);

        // Bug: Purge() iterates over ALL files including .json files.
        // When FilePair.FromContentFile() is called on a .json file,
        // it creates a pair where Content=Meta (both .json), causing
        // PurgeItem() to fail when trying to move the same file twice.
        httpCache.Purge();

        // After purge, all cache files should be deleted
        var remainingFiles = Directory.GetFiles(cacheDirectory);
        AreEqual(0, remainingFiles.Length);
    }

    [Test]
    public void Default_ConcurrentAccess_IsThreadSafe()
    {
        // Lazy<T> ensures thread-safe initialization without leaking instances.
        // All threads get the same instance and only one HttpCache + HttpClient is created.

        const int threadCount = 10;
        var barrier = new Barrier(threadCount);
        var instances = new HttpCache[threadCount];

        var threads = Enumerable.Range(0, threadCount)
            .Select(_ =>
                new Thread(() =>
                {
                    // All threads start at the same time
                    barrier.SignalAndWait();
                    instances[_] = HttpCache.Default;
                }))
            .ToList();

        threads.ForEach(_ => _.Start());
        threads.ForEach(_ => _.Join());

        // All threads should get the same instance
        var firstInstance = instances[0];
        True(instances.All(_ => ReferenceEquals(_, firstInstance)),
            "All threads should receive the same Default instance");
    }
}
#endif