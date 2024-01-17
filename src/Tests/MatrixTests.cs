using System.Net;
using System.Net.Http.Headers;
using Replicant;

//TODO: can LastModified or Expires for a NotModified?
public class MatrixTests
{
    static VerifySettings sharedSettings;
    static DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    static DateTimeOffset inPast = now.AddDays(-1);
    static DateTimeOffset inFuture = now.AddDays(1);
    static DateTimeOffset?[] expiries = [null, inFuture];
    static DateTimeOffset?[] mods = [null, now, inPast];

    static MatrixTests()
    {
        sharedSettings = new();
        sharedSettings.UseDirectory("../MatrixResults");
    }

    public static IEnumerable<object?[]> DataForIntegration()
    {
        foreach (var staleIfError in new[] {true, false})
        foreach (var modDate in mods)
        foreach (var expiry in expiries)
        foreach (var etag in etagStrings)
        foreach (var response in Responses())
        {
            yield return
            [
                new StoredData(expiry, modDate, etag),
                response,
                staleIfError
            ];
        }

        foreach (var staleIfError in new[] {true, false})
        foreach (var response in Responses())
        {
            yield return
            [
                null,
                response,
                staleIfError
            ];
        }
    }

    [Theory]
    [MemberData(nameof(DataForIntegration))]
    public async Task Integration(
        StoredData? data,
        HttpResponseMessageEx response,
        bool useStale)
    {
        string fileName;
        if (data == null)
        {
            fileName = $"Int_{response}_useStale={useStale}";
        }
        else
        {
            fileName = $"Int_{response}_useStale={useStale}_exp={data.Expiry:yyyyMMdd}_mod={data.Modified:yyyyMMdd}_tag={data.Etag?.Replace('/', '_').Replace('"', '_')}";
        }

        var settings = new VerifySettings(sharedSettings);
        settings.UseFileName(fileName);

        var directory = Path.Combine(Path.GetTempPath(), $"HttpCacheTests{Namer.Runtime}{fileName}");
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        try
        {
            await using var cache = new HttpCache(directory, new MockHttpClient(response));
            cache.Purge();
            if (data != null)
            {
                await cache.AddItemAsync("http://uri", "content", data.Expiry, data.Modified, data.Etag);
            }

            var result = await cache.DownloadAsync("http://uri", useStale);
            await Verify(result, settings);
        }
        catch (HttpRequestException exception)
        {
            await Verify(exception, settings);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [MemberData(nameof(StatusForMessageData))]
    public async Task StatusForMessage(HttpResponseMessageEx response, bool useStale)
    {
        var fileName = $"Status_{response}_useStale={useStale}";
        var settings = new VerifySettings(sharedSettings);
        settings.UseFileName(fileName);

        try
        {
            await Verify(response.GetCacheStatus(useStale), settings);
        }
        catch (HttpRequestException exception)
        {
            await Verify(exception, settings);
        }
        finally
        {
            response.Dispose();
        }
    }

    public static IEnumerable<object[]> StatusForMessageData()
    {
        foreach (var staleIfError in new[] {true, false})
        foreach (var response in Responses())
        {
            yield return
            [
                response,
                staleIfError
            ];
        }
    }

    static IEnumerable<HttpResponseMessageEx> Responses()
    {
        yield return new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("")
        };
        foreach (var webEtag in etags)
        foreach (var cacheControl in cacheControls)
        {
            var response = new HttpResponseMessageEx(HttpStatusCode.NotModified)
            {
                Content = new StringContent("")
            };
            if (!webEtag.IsEmpty)
            {
                response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
            }

            response.Headers.CacheControl = cacheControl;
            yield return response;
        }

        foreach (var webExpiry in expiries)
        {
            foreach (var webMod in mods)
            foreach (var webEtag in etags)
            foreach (var cacheControl in cacheControls)
            {
                var response = new HttpResponseMessageEx(HttpStatusCode.OK)
                {
                    Content = new StringContent("a")
                };
                response.Content.Headers.LastModified = webMod;
                response.Content.Headers.Expires = webExpiry;
                if (!webEtag.IsEmpty)
                {
                    response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
                }

                response.Headers.CacheControl = cacheControl;
                yield return response;
            }
        }
    }

    static Etag[] etags =
    [
        new("\"tag\"", "Stag", false),
        new("W/\"tag\"", "Wtag", false),
        Etag.Empty
    ];

    static string?[] etagStrings =
    [
        "\"tag\"",
        "W/\"tag\"",
        null
    ];

    static CacheControlHeaderValue[] cacheControls =
    [
        new()
        {
            Public = true,
            MaxAge = TimeSpan.FromDays(1)
        },
        new()
        {
            Private = true,
            MaxAge = TimeSpan.FromDays(1)
        },
        new()
        {
            NoCache = true,
        },
        new()
        {
            NoStore = true,
        }
    ];
}