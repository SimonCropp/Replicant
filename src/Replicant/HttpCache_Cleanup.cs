﻿using System;
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
        
        /// <inheritdoc/>
        public virtual void Purge()
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var pair = FilePair.FromContentFile(file!);
                pair.PurgeItem();
            }
        }
        
        /// <inheritdoc/>
        public virtual void PurgeOld()
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