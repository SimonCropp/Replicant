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
            if (maxEntries < 100)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries), "maxEntries must be greater than 100");
            }

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

            timer = new Timer(_ => PurgeOld(), null, ignoreTimeSpan, purgeInterval);
        }

        void PurgeOld()
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

        public async Task<(string path, CacheStatus status)> DownloadFile(string uri)
        {
            var file = Path.Combine(directory, $"{Hash.Compute(uri)}.bin");

            if (File.Exists(file))
            {
                var fileTimestamp = Timestamp.GetTimestamp(file);
                if (fileTimestamp.Expiry > DateTime.UtcNow)
                {
                    return (file, CacheStatus.Hit);
                }
            }

            Timestamp webTimeStamp;
            using (HttpRequestMessage request = new(HttpMethod.Head, uri))
            {
                using var headResponse = await client.SendAsync(request);
                headResponse.EnsureSuccessStatusCode();

                webTimeStamp = Timestamp.GetTimestamp(headResponse);
                if (File.Exists(file))
                {
                    var fileTimestamp = Timestamp.GetTimestamp(file);
                    if (fileTimestamp.LastModified == webTimeStamp.LastModified)
                    {
                        return (file,CacheStatus.Hit);
                    }

                    File.Delete(file);
                }
            }

            using var response = await client.GetAsync(uri);
            await using var httpStream = await response.Content.ReadAsStreamAsync();
            await using (FileStream fileStream = new(file, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await httpStream.CopyToAsync(fileStream);
            }

            webTimeStamp = Timestamp.GetTimestamp(response);

            Timestamp.SetTimestamp(file, webTimeStamp);
            return (file,CacheStatus.Miss);
        }

        public async Task<string> String(string uri)
        {
            var result = await DownloadFile(uri);
            return await File.ReadAllTextAsync(result.path);
        }

        public async Task<byte[]> Bytes(string uri)
        {
            var result = await DownloadFile(uri);
            return await File.ReadAllBytesAsync(result.path);
        }

        public async Task<Stream> Stream(string uri)
        {
            var result = await DownloadFile(uri);
            return File.OpenRead(result.path);
        }

        public void Dispose()
        {
            client.Dispose();
            timer.Dispose();
        }

        public async Task ToStream(string uri, Stream stream)
        {
            var result = await DownloadFile(uri);
            await using var fileStream = FileEx.OpenRead(result.path);
            await fileStream.CopyToAsync(stream);
        }

        public async Task ToFile(string uri, string path)
        {
            var result = await DownloadFile(uri);
            File.Copy(result.path, path, true);
        }

        public void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
        }
    }
}