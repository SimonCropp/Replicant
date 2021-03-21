using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Replicant
{
    public partial class HttpCache
    {
        int maxEntries;

        Timer timer;
        static TimeSpan purgeInterval = TimeSpan.FromMinutes(10);
        static TimeSpan ignoreTimeSpan = TimeSpan.FromMilliseconds(-1);

        void PauseAndPurgeOld()
        {
            timer.Change(ignoreTimeSpan, ignoreTimeSpan);
            try
            {
                PurgeOld();
            }
            finally
            {
                timer.Change(purgeInterval, ignoreTimeSpan);
            }
        }

        /// <summary>
        /// Purge all items from the cache.
        /// </summary>
        public void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var pair = FilePair.FromContentFile(file!);
                pair.PurgeItem();
            }
        }

        /// <summary>
        /// Purge old items based on maxEntries.
        /// </summary>
        public void PurgeOld()
        {
            foreach (var file in new DirectoryInfo(directory)
                .GetFiles("*_*_*.bin")
                .OrderByDescending(x => x.LastAccessTime)
                .Skip(maxEntries))
            {
                var pair = FilePair.FromContentFile(file!);
                pair.PurgeItem();
            }
        }
    }
}