using System.Globalization;

readonly struct Timestamp
{
    public DateTimeOffset? Expiry { get; }
    public DateTimeOffset Modified { get; }
    public Etag Etag { get; }
    public string UriHash { get; }
    public string Prefix { get; }
    public string ContentFileName { get; }
    public string MetaFileName { get; }

    public Timestamp(DateTimeOffset? expiry, DateTimeOffset modified, Etag etag, string uriHash)
    {
        Expiry = expiry;
        Modified = modified;
        Etag = etag;
        UriHash = uriHash;
        Prefix = $"{uriHash}_{modified:yyyy-MM-ddTHHmmss}_{etag.ForFile}.";
        ContentFileName = $"{Prefix}bin";
        MetaFileName = $"{Prefix}json";
    }

    public void ApplyHeadersToRequest(HttpRequestMessage request)
    {
        request.Headers.IfModifiedSince = Modified;
        request.AddIfNoneMatch(Etag);
    }

    public static Timestamp FromResponse(Uri uri, HttpResponseMessage response)
    {
        var now = DateTime.UtcNow;
        var hash = Hash.Compute(uri.AbsoluteUri);
        var etag = Etag.FromResponse(response);
        var expiry = response.GetExpiry(now);
        var modified = response.GetLastModified(now);
        return new(expiry, modified, etag, hash);
    }

    public static Timestamp FromPath(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var indexOf = file.IndexOf('_');

        var urlHash = file[..indexOf];

        var modifiedPart = file.Substring(indexOf + 1, 17);
        var modified = DateTimeOffset.ParseExact(modifiedPart, "yyyy-MM-ddTHHmmss", null, DateTimeStyles.AssumeUniversal);

        var etagPart = file[(indexOf + 19)..];
        var etag = Etag.FromFilePart(etagPart);

        DateTime? expiry = File.GetLastWriteTimeUtc(path);
        if (expiry == FileEx.MinFileDate)
        {
            expiry = null;
        }

        return new(expiry, modified, etag, urlHash);
    }
}