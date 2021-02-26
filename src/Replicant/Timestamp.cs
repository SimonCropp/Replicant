using System;
using System.IO;
using System.Net.Http;

class Timestamp
{
    public DateTime? Expiry;
    public DateTime LastModified;
    public string? ETag;
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
        timestamp.LastModified = DateTime.ParseExact(lastModifiedPart, "yyyy-MM-ddTHHmmss", null);

        timestamp.ETag = file[(indexOf + 19)..];

        var expiry = File.GetLastWriteTimeUtc(path);
        if (expiry != FileEx.MinFileDate)
        {
            timestamp.Expiry = expiry;
        }

        return timestamp;
    }
}