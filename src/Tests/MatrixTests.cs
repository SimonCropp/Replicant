using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using VerifyTests;
using VerifyXunit;
using Xunit;

//TODO: can LastModified or Expires for a NotModified?
[UsesVerify]
public class MatrixTests
{
    static VerifySettings sharedSettings;
    static DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    static DateTimeOffset inPast = now.AddDays(-1);
    static DateTimeOffset inFuture = now.AddDays(1);

    static MatrixTests()
    {
        sharedSettings = new VerifySettings();
        sharedSettings.UseDirectory("../MatrixResults");
    }

    public static IEnumerable<object[]> DataForIntegration()
    {
        foreach (var staleIfError in new[] {true, false})
        foreach (var response in Responses())
        {
            yield return new object[]
            {
                response,
                staleIfError
            };
        }
    }

    [Theory]
    [MemberData(nameof(StatusForMessageData))]
    public async Task StatusForMessage(HttpResponseMessageEx response, bool staleIfError)
    {
        var fileName = BuildStatusForMessageFileName(response, staleIfError);
        var settings = new VerifySettings(sharedSettings);
        settings.UseFileName(fileName);

        try
        {
            await Verifier.Verify(response.CacheStatus(staleIfError), settings);
        }
        catch (HttpRequestException exception)
        {
            await Verifier.Verify(exception, settings);
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
            yield return new object[]
            {
                response,
                staleIfError
            };
        }
    }

    public static IEnumerable<HttpResponseMessageEx> Responses()
    {
        DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var inPast = now.AddDays(-1);
        var inFuture = now.AddDays(1);
        yield return new(HttpStatusCode.BadRequest);
        foreach (var webEtag in etags)
        foreach (var cacheControl in cacheControls)
        {
            HttpResponseMessageEx response = new(HttpStatusCode.NotModified);
            if (!webEtag.IsEmpty)
            {
                response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
            }

            response.Headers.CacheControl = cacheControl;
            yield return response;
        }

        foreach (var webExpiry in new DateTimeOffset?[] {null, inFuture})
        foreach (var webMod in new DateTimeOffset?[] {null, now, inPast})
        foreach (var webEtag in etags)
        foreach (var cacheControl in cacheControls)
        {
            HttpResponseMessageEx response = new(HttpStatusCode.OK)
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

    static string BuildStatusForMessageFileName(HttpResponseMessage response, bool staleIfError)
    {
        return $"StatusForMessage_{response}_staleIfError={staleIfError}";
    }

    static Etag[] etags = new Etag[]
    {
        new("\"tag\"", "Stag", false),
        Etag.Empty,
    };

    static CacheControlHeaderValue[] cacheControls = new CacheControlHeaderValue[]
    {
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
    };
}