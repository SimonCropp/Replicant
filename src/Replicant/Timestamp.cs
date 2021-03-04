using System;
using System.Globalization;
using System.IO;
using System.Net.Http;

readonly struct Timestamp
{
    public DateTimeOffset? Expiry { get; }
    public DateTimeOffset Modified { get; }
    public Etag Etag { get; }
    public string UriHash { get; }
    public string ContentFileName { get; }
    public string MetaFileName { get; }

    public Timestamp(DateTimeOffset? expiry, DateTimeOffset modified, Etag etag, string uriHash)
    {
        Expiry = expiry;
        Modified = modified;
        Etag = etag;
        UriHash = uriHash;
        var prefix = $"{uriHash}_{modified:yyyy-MM-ddTHHmmss}_{etag.ForFile}.";
        ContentFileName = $"{prefix}bin";
        MetaFileName = $"{prefix}json";
    }

    public void ApplyHeadersToRequest(HttpRequestMessage request)
    {
        request.Headers.IfModifiedSince = Modified;
        request.AddIfNoneMatch(Etag);
    }

    public static Timestamp FromResponse(string uri, HttpResponseMessage response)
    {
        var now = DateTime.UtcNow;
        var hash = Hash.Compute(uri);
        var etag = Etag.FromResponse(response);
        var expiry = response.GetExpiry(now);
        var modified = response.GetLastModified(now);
        return new Timestamp(expiry, modified, etag, hash);
    }

    public static Timestamp FromPath(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var indexOf = file.IndexOf('_');

        var urlHash = file.Substring(0, indexOf);

        var modifiedPart = file.Substring(indexOf + 1, 17);
        var modified = DateTimeOffset.ParseExact(modifiedPart, "yyyy-MM-ddTHHmmss", null, DateTimeStyles.AssumeUniversal);

        var etagPart = file.Substring(indexOf + 19);
        var etag = Etag.FromFilePart(etagPart);

        DateTime? expiry = File.GetLastWriteTimeUtc(path);
        if (expiry == FileEx.MinFileDate)
        {
            expiry = null;
        }

        return new(expiry, modified, etag, urlHash);
    }
}