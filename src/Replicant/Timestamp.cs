using System;
using System.Globalization;
using System.IO;

class Timestamp
{
    public DateTimeOffset? Expiry;
    public DateTimeOffset LastModified;
    public string? ETag;
    public bool? WeakEtag;
    public string UrlHash = null!;

    public static Timestamp Get(string path)
    {
        var file = Path.GetFileNameWithoutExtension(path);
        var indexOf = file.IndexOf('_');

        Timestamp timestamp = new()
        {
            UrlHash = file[..indexOf]
        };

        var lastModifiedPart = file.Substring(indexOf + 1, 17);
        timestamp.LastModified = DateTimeOffset.ParseExact(lastModifiedPart, "yyyy-MM-ddTHHmmss", null, DateTimeStyles.AssumeUniversal);

        var etagPart = file[(indexOf + 19)..];
        if (etagPart != string.Empty)
        {
            timestamp.WeakEtag = etagPart.StartsWith("W");
            timestamp.ETag = etagPart[1..];
        }

        var expiry = File.GetLastWriteTimeUtc(path);
        if (expiry != FileEx.MinFileDate)
        {
            timestamp.Expiry = expiry;
        }

        return timestamp;
    }

}