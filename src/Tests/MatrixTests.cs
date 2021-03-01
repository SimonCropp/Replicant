using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Replicant;
using VerifyTests;
using VerifyXunit;
using Xunit;

//TODO: can LastModified or Expires for a NotModified?
[UsesVerify]
public class MatrixTests
{
    static VerifySettings sharedSettings;

    static MatrixTests()
    {
        sharedSettings = new VerifySettings();
        sharedSettings.UseDirectory("../MatrixResults");
    }

    [Theory]
    [MemberData(nameof(StatusForMessageData))]
    internal async Task StatusForMessage(HttpResponseMessage response, bool staleIfError)
    {
        var fileName = BuildStatusForMessageFileName(response, staleIfError);
        var settings = new VerifySettings(sharedSettings);
        settings.UseFileName(fileName);

        try
        {
            await Verifier.Verify(HttpCache.StatusForMessage(response, staleIfError), settings);
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

    public static IEnumerable<HttpResponseMessage> Responses()
    {
        DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var inPast = now.AddDays(-1);
        var inFuture = now.AddDays(1);
        yield return new(HttpStatusCode.BadRequest);
        foreach (var webEtag in etags)
        foreach (var cacheControl in cacheControls)
        {
            HttpResponseMessage response = new(HttpStatusCode.NotModified);
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
            HttpResponseMessage response = new(HttpStatusCode.OK);
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
        var builder = new StringBuilder($"StatusForMessage_status_{response.StatusCode}_staleIfError={staleIfError}_");
        var cacheControl = response.Headers.CacheControl;
        if (cacheControl == null)
        {
            builder.Append("cache=null_");
        }
        else
        {
            builder.Append($"cache={cacheControl.ToString().Replace(", ", ",")}_");
        }

        var webExpires = response.Content.Headers.Expires;
        if (webExpires == null)
        {
            builder.Append("expires=null_");
        }
        else
        {
            builder.Append($"expires={webExpires.Value:yyyyMMdd}_");
        }

        var webMod = response.Content.Headers.LastModified;
        if (webMod == null)
        {
            builder.Append("mod=null_");
        }
        else
        {
            builder.Append($"mod={webMod.Value:yyyyMMdd}_");
        }

        var webEtag = response.Headers.ETag;
        if (webEtag == null)
        {
            builder.Append("mod=null_");
        }
        else
        {
            var etag = Etag.FromResponse(response);
            if (etag.IsEmpty)
            {
                builder.Append("mod=empty_");
            }
            else
            {
                builder.Append($"mod={etag.ForFile.Substring(1)}_");
            }
        }

        return builder.ToString().Trim('_');
    }

    //[Theory]
    //[MemberData(nameof(GetData))]
    //internal async Task ExistingFile(HttpResponseMessage response, Timestamp fromDisk)
    //{
    //    var fileName = BuildFileName(response, fromDisk);
    //    try
    //    {
    //        await Verifier.Verify("foo")
    //            .UseDirectory("../MatrixResults")
    //            .UseFileName(fileName);
    //    }
    //    finally
    //    {
    //        response.Dispose();
    //    }
    //}

    //static string BuildFileName(HttpResponseMessage response, Timestamp fromDisk)
    //{
    //    var builder = new StringBuilder();
    //    var cacheControl = response.Headers.CacheControl;
    //    if (cacheControl != null)
    //    {
    //        builder.Append($"wCache={cacheControl.ToString().Replace(", ", ",")}_");
    //    }

    //    var webExpires = response.Content.Headers.Expires;
    //    if (webExpires == null)
    //    {
    //        builder.Append("wExpires=null_");
    //    }
    //    else
    //    {
    //        builder.Append($"wExpires={webExpires.Value:yyyyMMdd}_");
    //    }

    //    var webMod = response.Content.Headers.LastModified;
    //    if (webMod == null)
    //    {
    //        builder.Append("wMod=null_");
    //    }
    //    else
    //    {
    //        builder.Append($"wMod={webMod.Value:yyyyMMdd}_");
    //    }

    //    var webEtag = response.Headers.ETag;
    //    if (webEtag == null)
    //    {
    //        builder.Append("wMod=null_");
    //    }
    //    else
    //    {
    //        var etag = Etag.FromResponse(response);
    //        if (etag.IsEmpty)
    //        {
    //            builder.Append("wMod=null_");
    //        }
    //        else
    //        {
    //            builder.Append($"wMod={etag.ForFile.Substring(1)}_");
    //        }
    //    }

    //    if (fromDisk.Expiry == null)
    //    {
    //        builder.Append("dExpiry=null_");
    //    }
    //    else
    //    {
    //        builder.Append($"dExpiry={fromDisk.Expiry.Value:yyyyMMdd}_");
    //    }

    //    builder.Append($"dMod={fromDisk.Modified:yyyyMMdd}_");

    //    if (fromDisk.Etag.IsEmpty)
    //    {
    //        builder.Append("dEtag=null_");
    //    }
    //    else
    //    {
    //        builder.Append($"dEtag={fromDisk.Etag.ForFile.Substring(1)}_");
    //    }

    //    return builder.ToString().Trim('_');
    //}

    ////https://web.dev/http-cache/

    //public static IEnumerable<object[]> GetData()
    //{
    //    DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    //    var inPast = now.AddDays(-1);
    //    var inFuture = now.AddDays(1);
    //    var uri = "foo";
    //    var hash = Hash.Compute(uri);
    //    foreach (var fileExpiry in new DateTimeOffset?[] {null, now, inPast, inFuture})
    //    foreach (var fileModified in new[] {now, inPast})
    //    foreach (var fileEtag in etags)
    //    {
    //        Timestamp timestamp = new(fileExpiry, fileModified, fileEtag, hash);
    //        HttpResponseMessage response = new(HttpStatusCode.BadRequest);
    //        yield return new object[]
    //        {
    //            response,
    //            timestamp
    //        };
    //    }
    //    foreach (var webEtag in etags)
    //    foreach (var cacheControl in cacheControls)
    //    foreach (var fileExpiry in new DateTimeOffset?[] {null, now, inPast, inFuture})
    //    foreach (var fileModified in new[] {now, inPast})
    //    foreach (var fileEtag in etags)
    //    {
    //        Timestamp timestamp = new(fileExpiry, fileModified, fileEtag, hash);
    //        HttpResponseMessage response = new(HttpStatusCode.NotModified);
    //        //TODO: can LastModified or Expires for a NotModified?
    //        //response.Content.Headers.LastModified = webMod;
    //        //response.Content.Headers.Expires = webExpiry;
    //        if (!webEtag.IsEmpty)
    //        {
    //            response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
    //        }
    //        response.Headers.CacheControl = cacheControl;
    //        yield return new object[]
    //        {
    //            response,
    //            timestamp
    //        };
    //    }
    //    foreach (var webExpiry in new DateTimeOffset?[] {null, inFuture})
    //    foreach (var webMod in new DateTimeOffset?[] {null, now, inPast})
    //    foreach (var webEtag in etags)
    //    foreach (var cacheControl in cacheControls)
    //    foreach (var fileExpiry in new DateTimeOffset?[] {null, now, inPast, inFuture})
    //    foreach (var fileModified in new[] {now, inPast})
    //    foreach (var fileEtag in etags)
    //    {
    //        Timestamp timestamp = new(fileExpiry, fileModified, fileEtag, hash);
    //        HttpResponseMessage response = new(HttpStatusCode.OK);
    //        response.Content.Headers.LastModified = webMod;
    //        response.Content.Headers.Expires = webExpiry;
    //        if (!webEtag.IsEmpty)
    //        {
    //            response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
    //        }
    //        response.Headers.CacheControl = cacheControl;
    //        yield return new object[]
    //        {
    //            response,
    //            timestamp
    //        };
    //    }
    //}

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