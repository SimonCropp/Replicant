using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

static class Downloader
{
    static string cache;

    static Downloader()
    {
        cache = Path.Combine(Path.GetTempPath(), "MarkdownSnippets");
        Directory.CreateDirectory(cache);
        foreach (var file in new DirectoryInfo(cache)
            .GetFiles()
            .OrderByDescending(x => x.LastWriteTime)
            .Skip(100))
        {
            file.Delete();
        }
    }

    static HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<(bool success, string? path)> DownloadFile(string uri)
    {
        var file = Path.Combine(cache, FileNameFromUrl.ConvertToFileName(uri));

        if (File.Exists(file))
        {
            var fileTimestamp = Timestamp.GetTimestamp(file);
            if (fileTimestamp.Expiry > DateTime.UtcNow)
            {
                return (true, file);
            }
        }

        Timestamp webTimeStamp;
        using (HttpRequestMessage request = new(HttpMethod.Head, uri))
        {
            using var headResponse = await httpClient.SendAsync(request);
            if (headResponse.StatusCode != HttpStatusCode.OK)
            {
                return (false, null);
            }

            webTimeStamp = Timestamp.GetTimestamp(headResponse);

            if (File.Exists(file))
            {
                var fileTimestamp = Timestamp.GetTimestamp(file);
                if (fileTimestamp.LastModified == webTimeStamp.LastModified)
                {
                    return (true, file);
                }

                File.Delete(file);
            }
        }

        using var response = await httpClient.GetAsync(uri);
        using var httpStream = await response.Content.ReadAsStreamAsync();
        using (FileStream fileStream = new(file, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await httpStream.CopyToAsync(fileStream);
        }

        webTimeStamp = Timestamp.GetTimestamp(response);

        Timestamp.SetTimestamp(file, webTimeStamp);
        return (true, file);
    }

    public static async Task<(bool success, string? content)> DownloadContent(string uri)
    {
        var (success, path) = await DownloadFile(uri);
        if (success)
        {
            return (true, File.ReadAllText(path));
        }
        return (false, null);
    }
}