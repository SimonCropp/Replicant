using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

[UsesVerify]
public class MatrixTests
{
    [Theory]
    [MemberData(nameof(GetData))]
    internal async Task ExistingFile(HttpResponseMessage response, Timestamp fromDisk)
    {
        var fileName = BuildFileName(response, fromDisk);
        try
        {
            await Verifier.Verify("foo")
                .UseFileName(fileName);
        }
        finally
        {
            response.Dispose();
        }
    }

    static string BuildFileName(HttpResponseMessage response, Timestamp fromDisk)
    {
        var builder = new StringBuilder();
        var cacheControl = response.Headers.CacheControl;
        if (cacheControl != null)
        {
            builder.Append($"cacheControl={cacheControl.ToString().Replace(", ", ",")}_");
        }

        var webExpires = response.Content.Headers.Expires;
        if (webExpires != null)
        {
            builder.Append($"webExpires={webExpires.Value:yyyyMMdd}_");
        }

        var webMod = response.Content.Headers.LastModified;
        if (webMod != null)
        {
            builder.Append($"webMod={webMod.Value:yyyyMMdd}_");
        }

        var webEtag = response.Headers.ETag;
        if (webEtag != null)
        {
            var etag = Etag.FromResponse(response);
            if (!etag.IsEmpty)
            {
                builder.Append($"webMod={etag.ForFile}_");
            }
        }

        if (fromDisk.Expiry != null)
        {
            builder.Append($"diskExpiry={fromDisk.Expiry.Value:yyyyMMdd}_");
        }

        builder.Append($"_diskMod={fromDisk.Modified:yyyyMMdd}_");

        if (!fromDisk.Etag.IsEmpty)
        {
            builder.Append($"diskEtag={fromDisk.Etag.ForFile}_");
        }

        return builder.ToString().Trim('_');
    }

    //https://web.dev/http-cache/

    public static IEnumerable<object[]> GetData()
    {
        DateTimeOffset now = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var inPast = now.AddDays(-1);
        var inFuture = now.AddDays(1);
        var uri = "foo";
        var hash = Hash.Compute(uri);
        foreach (var statusCode in new[] {HttpStatusCode.OK, HttpStatusCode.NotModified, HttpStatusCode.BadRequest})
        foreach (var webExpiry in new DateTimeOffset?[] {null, now, inPast, inFuture})
        foreach (var webMod in new DateTimeOffset?[] {null, now, inPast, inFuture})
        foreach (var webEtag in etags)
        foreach (var cacheControl in cacheControls)
        foreach (var fileExpiry in new DateTimeOffset?[] {null, now, inPast, inFuture})
        foreach (var fileModified in new[] {now, inPast, inFuture})
        foreach (var fileEtag in etags)
        {
            Timestamp timestamp = new(fileExpiry, fileModified, fileEtag, hash);
            HttpResponseMessage response = new(statusCode);
            response.Content.Headers.LastModified = webMod;
            response.Content.Headers.Expires = webExpiry;
            if (!webEtag.IsEmpty)
            {
                response.Headers.TryAddWithoutValidation("ETag", webEtag.ForWeb);
            }
            response.Headers.CacheControl = cacheControl;
            yield return new object[]
            {
                response,
                timestamp
            };
        }
    }

    static Etag[] etags = new Etag[]
    {
        new("\"thetag\"", "Sthetag", false),
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