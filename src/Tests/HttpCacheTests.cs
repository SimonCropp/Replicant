using System;
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
        var content = await httpCache.Download("https://httpbin.org/status/200");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task DuplicateSlowDownloads()
    {
        var task = httpCache.Download("https://httpbin.org/delay/1");
        await Task.Delay(100);
        var result = await httpCache.Download("https://httpbin.org/delay/1");

        await task;

        using var httpResponseMessage = result.AsResponseMessage();
        await Verifier.Verify(httpResponseMessage);
    }
#if DEBUG
    //TODO: debug these on mac
    [Fact]
    public async Task PurgeOldWhenContentFileLocked()
    {
        var result = await httpCache.Download("https://httpbin.org/status/200");

        using (result.AsResponseMessage())
        {
            HttpCache.PurgeItem(result.ContentPath);
            Assert.True(File.Exists(result.ContentPath));
            Assert.True(File.Exists(result.MetaPath));
        }
    }

    [Fact]
    public async Task PurgeOldWhenMetaFileLocked()
    {
        var result = await httpCache.Download("https://httpbin.org/status/200");

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
        var result = await httpCache.Download(uri);
        using (result.AsResponseMessage())
        {
            result = await httpCache.Download(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verifier.Verify(httpResponseMessage);
    }

    [Fact]
    public async Task LockedMetaFile()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        var result = await httpCache.Download(uri);
        await using (new FileStream(result.MetaPath!, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            result = await httpCache.Download(uri);
        }

        using var httpResponseMessage = result.AsResponseMessage();
        await Verifier.Verify(httpResponseMessage);
    }

    [Fact]
    public async Task FullHttpResponseMessage()
    {
        #region FullHttpResponseMessage

        using var response = await httpCache.Response("https://httpbin.org/status/200");

        #endregion

        await Verifier.Verify(response);
    }

    [Fact]
    public async Task Etag()
    {
        var uri = "https://httpbin.org/etag/{etag}";
        await httpCache.Download(uri);
        var content = await httpCache.Download(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WeakEtag()
    {
        var uri = "https://www.wikipedia.org/";
        HttpResponseMessage newMessage;
        using (var content = await httpCache.Response(uri))
        {
            newMessage = new(HttpStatusCode.OK);
            newMessage.Headers.ETag = content.Headers.ETag;
        }

        await httpCache.AddItem(uri, newMessage);
        using var content2 = await httpCache.Response(uri);
        await Verifier.Verify(content2);
    }

    [Fact]
    public async Task CacheControlMaxAge()
    {
        var uri = "https://httpbin.org/cache/20";
        await httpCache.Download(uri);
        var content = await httpCache.Download(uri);
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task WithContent()
    {
        var content = await httpCache.Download("https://httpbin.org/json");
        await Verifier.Verify(content);
    }

    [Fact]
    public async Task String()
    {
        #region string

        var content = await httpCache.String("https://httpbin.org/json");

        #endregion

        await Verifier.Verify(content);
    }

    [Fact]
    public async Task Bytes()
    {
        #region bytes

        var bytes = await httpCache.Bytes("https://httpbin.org/json");

        #endregion

        await Verifier.Verify(Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Stream()
    {
        #region stream

        await using var stream = await httpCache.Stream("https://httpbin.org/json");

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

            await httpCache.ToFile("https://httpbin.org/json", targetFile);

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

        await httpCache.ToStream("https://httpbin.org/json", targetStream);

        #endregion

        await Verifier.Verify(targetStream).UseExtension("txt");
    }

    [Fact]
    public async Task Callback()
    {
        var uri = "https://httpbin.org/json";

        #region Callback

        var content = await httpCache.String(
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
        return Verifier.ThrowsTask(() => httpCache.String("https://httpbin.org/status/404"));
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
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => httpCache.String(uri));
        await Verifier.Verify(exception.Message);
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
        await httpCache.AddItem(uri, response);

        #endregion

        await Verifier.Verify(httpCache.Download(uri, true));
    }

    [Fact]
    public Task ServerError()
    {
        return Verifier.ThrowsTask(() => httpCache.String("https://httpbin.org/status/500"));
    }

    [Fact]
    public async Task ServerErrorDontUseStale()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent("foo")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItem(uri, response);
        await Verifier.ThrowsTask(() => httpCache.String(uri));
    }

    [Fact]
    public async Task ServerErrorUseStale()
    {
        using HttpResponseMessage httpResponseMessage = new(HttpStatusCode.OK)
        {
            Content = new StringContent("content")
        };
        var uri = "https://httpbin.org/status/500";
        await httpCache.AddItem(uri, httpResponseMessage);

        #region useStaleOnError

        var content = httpCache.String(uri, useStaleOnError: true);

        #endregion

        await Verifier.Verify(content);
    }
}