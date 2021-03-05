using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Replicant;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class HttpCacheTests
{
    HttpCache httpCache;
    static string CachePath = Path.Combine(Path.GetTempPath(), "DownloadTests");

    public HttpCacheTests()
    {
        httpCache = new(CachePath);
        httpCache.Purge();
    }

    async Task Construction(string cacheDirectory)
    {
        #region Construction

        HttpCache httpCache = new(
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

    [Fact]
    public void DependencyInjection()
    {
        #region DependencyInjection

        ServiceCollection services = new();
        services.AddSingleton(_ => new HttpCache(CachePath));

        using var provider = services.BuildServiceProvider();
        var httpCache = provider.GetRequiredService<HttpCache>();
        Assert.NotNull(httpCache);

        #endregion
    }

    [Fact]
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
        Assert.NotNull(httpCache);

        #endregion
    }

    [Fact]
    public async Task EmptyContent()
    {
        var content = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task DuplicateSlowDownloads()
    {
        var task = httpCache.DownloadAsync("https://httpbin.org/delay/1");
        await Task.Delay(100);
        var result2 = await httpCache.DownloadAsync("https://httpbin.org/delay/1");

        var result1 = await task;

        Assert.True(CacheStatus.Stored == result1.Status ||
                    CacheStatus.Stored == result2.Status);
    }

#if DEBUG
    //TODO: debug these on mac
    [Fact]
    public async Task PurgeOldWhenContentFileLocked()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");
        using (result.AsResponseMessage())
        {
            HttpCache.PurgeItem(result.ContentPath!);
            Assert.True(File.Exists(result.ContentPath));
            Assert.True(File.Exists(result.MetaPath));
        }
    }

    [Fact]
    public async Task PurgeOldWhenMetaFileLocked()
    {
        var result = await httpCache.DownloadAsync("https://httpbin.org/status/200");

        using (result.AsResponseMessage())
        {
            HttpCache.PurgeItem(result.ContentPath!);
            Assert.True(File.Exists(result.ContentPath));
            Assert.True(File.Exists(result.MetaPath));
        }
    }
#endif

    [Fact]
    public async Task LockedContentFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);
        using (result.AsResponseMessage())
        {
            result = await httpCache.DownloadAsync(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verifier.Verify(httpResponseMessage);
    }

    [Fact]
    public async Task LockedMetaFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.DownloadAsync(uri);
        using (new FileStream(result.MetaPath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await httpCache.DownloadAsync(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verifier.Verify(httpResponseMessage);
    }

    [Fact]
    public async Task FullHttpResponseMessageAsync()
    {
        #region FullHttpResponseMessage

        using var response = await httpCache.ResponseAsync("https://httpbin.org/status/200");

        #endregion

        await Verifier.Verify(response);
    }
    [Fact]
    public async Task FullHttpResponseMessage()
    {
        using var response = httpCache.Response("https://httpbin.org/status/200");
        await Verifier.Verify(response);
    }

    [Fact]
    public async Task NoCache()
    {
        using var result = await httpCache.DownloadAsync("https://httpbin.org/response-headers?Cache-Control=no-cache");
        Assert.NotNull(result.Response);
        await Verifier.Verify(result);
    }

    [Fact]
    public async Task Etag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WeakEtag()
    {
        var uri = "https://www.wikipedia.org/";
        HttpResponseMessage newMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("a"),
        };
        using (var content = await httpCache.ResponseAsync(uri))
        {
            newMessage.Headers.ETag = content.Headers.ETag;
        }

        await httpCache.AddItemAsync(uri, newMessage);
        using var content2 = await httpCache.ResponseAsync(uri);
        await Verifier.Verify(content2);
    }

    [Fact]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await httpCache.DownloadAsync(uri);
        var content = await httpCache.DownloadAsync(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WithContent()
    {
        var content = await httpCache.DownloadAsync("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public Task WithContentSync()
    {
        var content = httpCache.Download("https://httpbin.org/json");
        return Verifier.Verify(content);
    }

    [Fact]
    public async Task String()
    {
        #region string

        var content = await httpCache.StringAsync("https://httpbin.org/json");

        #endregion

        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Lines()
    {
        #region string

        List<string> lines = new();
        await foreach (var line in httpCache.LinesAsync("https://httpbin.org/json"))
        {
            lines.Add(line);
        }

        #endregion

        await Verifier.Verify(lines);
    }

    [Fact]
    public async Task Bytes()
    {
        #region bytes

        var bytes = await httpCache.BytesAsync("https://httpbin.org/json");

        #endregion

        await Verifier.Verify(Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Stream()
    {
        #region stream

        using var stream = await httpCache.StreamAsync("https://httpbin.org/json");

        #endregion

        await Verifier.Verify(stream).UseExtension("txt");
    }

    [Fact]
    public async Task ToFile()
    {
        var targetFile = FileEx.GetTempFileName("txt");
        try
        {
            #region ToFile

            await httpCache.ToFileAsync("https://httpbin.org/json", targetFile);

            #endregion

            await Verifier.VerifyFile(targetFile);
        }
        finally
        {
            File.Delete(targetFile);
        }
    }

    [Fact]
    public async Task ToStream()
    {
        MemoryStream targetStream = new();

        #region ToStream

        await httpCache.ToStreamAsync("https://httpbin.org/json", targetStream);

        #endregion

        await Verifier.Verify(targetStream).UseExtension("txt");
    }

    [Fact]
    public async Task Callback()
    {
        var uri = "https://httpbin.org/json";

        #region Callback

        var content = await httpCache.StringAsync(
            uri,
            messageCallback: message =>
            {
                message.Headers.Add("Key1", "Value1");
                message.Headers.Add("Key2", "Value2");
            });

        #endregion

        await Verifier.Verify(content);
    }

    [Fact]
    public Task NotFound()
    {
        return Verifier.ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/404"));
    }

    [Fact]
    public async Task Timeout()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(10)
        };
        httpCache = new(CachePath, httpClient);
        httpCache.Purge();
        var uri = "https://httpbin.org/delay/1";
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => httpCache.StringAsync(uri));
        await Verifier.Verify(exception.Message)
            .UniqueForRuntime();
    }

    [Fact]
    public async Task TimeoutUseStale()
    {
        HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        httpCache = new(CachePath, httpClient);
        httpCache.Purge();
        var uri = "https://httpbin.org/delay/1";

        #region AddItem

        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("the content")
        };
        await httpCache.AddItemAsync(uri, response);

        #endregion

        await Verifier.Verify(httpCache.DownloadAsync(uri, true));
    }

    [Fact]
    public Task ServerError()
    {
        return Verifier.ThrowsTask(() => httpCache.StringAsync("https://httpbin.org/status/500"));
    }

    [Fact]
    public async Task ServerErrorDontUseStale()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItemAsync(uri, response);
        await Verifier.ThrowsTask(() => httpCache.StringAsync(uri));
    }

    [Fact]
    public async Task ServerErrorUseStale()
    {
        using HttpResponseMessage httpResponseMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("content")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItemAsync(uri, httpResponseMessage);

        #region staleIfError

        var content = httpCache.StringAsync(uri, staleIfError: true);

        #endregion

        await Verifier.Verify(content);
    }
}