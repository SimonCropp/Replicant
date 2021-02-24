using System;
using System.IO;
using System.Net.Http;

class Timestamp
{
    static DateTime minFileDate = DateTime.FromFileTimeUtc(0);
    public DateTime? Expiry;
    public DateTime? LastModified;

    public static Timestamp GetTimestamp(HttpResponseMessage headResponse)
    {
        Timestamp timestamp = new();
        if (headResponse.Content.Headers.LastModified != null)
        {
            timestamp.LastModified = headResponse.Content.Headers.LastModified.Value.UtcDateTime;
        }

        if (headResponse.Content.Headers.Expires != null)
        {
            timestamp.Expiry = headResponse.Content.Headers.Expires.Value.UtcDateTime;
        }

        return timestamp;
    }

    public  static void SetTimestamp(string path, Timestamp timestamp)
    {
        File.SetCreationTimeUtc(path, timestamp.LastModified.GetValueOrDefault(DateTime.UtcNow));
        File.SetLastWriteTimeUtc(path, timestamp.Expiry.GetValueOrDefault(minFileDate));
    }

    public static Timestamp GetTimestamp(string path)
    {
        Timestamp timestamp = new();
        var creationTimeUtc = File.GetCreationTimeUtc(path);
        if (creationTimeUtc != minFileDate)
        {
            timestamp.LastModified = creationTimeUtc;
        }

        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        if (lastWriteTimeUtc != minFileDate)
        {
            timestamp.Expiry = lastWriteTimeUtc;
        }

        return timestamp;
    }
}