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
        var fileSpan = file.AsSpan();
        var indexOf = fileSpan.IndexOf('_');

        // Expected format: {hash}_{yyyy-MM-ddTHHmmss}_{etag}
        // Minimum length after first underscore: 17 (date) + 1 (underscore) = 18
        if (indexOf < 0 || fileSpan.Length < indexOf + 19)
        {
            throw new ArgumentException(
                $"Invalid cache filename format. Expected '{{hash}}_{{yyyy-MM-ddTHHmmss}}_{{etag}}', got '{file}'",
                nameof(path));
        }

        var urlHashSpan = fileSpan[..indexOf];
        var modifiedSpan = fileSpan.Slice(indexOf + 1, 17);
        var etagSpan = fileSpan[(indexOf + 19)..];

        if (!DateTimeOffset.TryParseExact(modifiedSpan, "yyyy-MM-ddTHHmmss".AsSpan(), null, DateTimeStyles.AssumeUniversal, out var modified))
        {
            throw new ArgumentException(
                $"Invalid date format in cache filename. Expected 'yyyy-MM-ddTHHmmss', got '{modifiedSpan.ToString()}'",
                nameof(path));
        }

        var etag = Etag.FromFilePart(etagSpan.ToString());

        DateTime? expiry = File.GetLastWriteTimeUtc(path);
        if (expiry == FileEx.MinFileDate || expiry == FileEx.OldMinFileDate)
        {
            expiry = null;
        }

        return new(expiry, modified, etag, urlHashSpan.ToString());
    }
}