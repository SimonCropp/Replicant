using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Replicant
{
    public class Download: IDisposable
    {
        string directory;
        int maxEntries;
        HttpClient client;
        Timer timer;
        static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
        static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);

        public Download(string directory, HttpClient? client = null, int maxEntries = 1000)
        {
            Guard.AgainstNullOrEmpty(directory, nameof(directory));
            this.directory = directory;
            this.maxEntries = maxEntries;
            if (client == null)
            {
                this.client = new()
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };
            }
            else
            {
                this.client = client;
            }

            Directory.CreateDirectory(directory);

            timer = new Timer(_ => Purge(), null, ignoreTimeSpan, purgeInterval);
        }

        void Purge()
        {
            timer.Change(ignoreTimeSpan, ignoreTimeSpan);
            try
            {
                foreach (var file in new DirectoryInfo(directory)
                    .GetFiles()
                    .OrderByDescending(x => x.LastAccessTime)
                    .Skip(maxEntries))
                {
                    file.Delete();
                }
            }
            finally
            {
                timer.Change(purgeInterval, ignoreTimeSpan);
            }
        }

        public async Task<(bool success, string? path)> DownloadFile(string uri)
        {
            var file = Path.Combine(directory, Hash.Compute(uri));

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
                using var headResponse = await client.SendAsync(request);
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

            using var response = await client.GetAsync(uri);
            using var httpStream = await response.Content.ReadAsStreamAsync();
            using (FileStream fileStream = new(file, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await httpStream.CopyToAsync(fileStream);
            }

            webTimeStamp = Timestamp.GetTimestamp(response);

            Timestamp.SetTimestamp(file, webTimeStamp);
            return (true, file);
        }

        public async Task<(bool success, string? content)> String(string uri)
        {
            var (success, path) = await DownloadFile(uri);
            if (success)
            {
                return (true, File.ReadAllText(path));
            }

            return (false, null);
        }

        public void Dispose()
        {
            client.Dispose();
            timer.Dispose();
        }
    }
}