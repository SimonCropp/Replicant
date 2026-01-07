namespace Replicant;

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
        foreach (var file in Directory.EnumerateFiles(directory, "*.bin"))
        {
            var pair = FilePair.FromContentFile(file);
            pair.PurgeItem();
        }
    }

    /// <inheritdoc/>
    public virtual void PurgeOld()
    {
        foreach (var file in Directory
            .EnumerateFiles(directory, "*_*_*.bin")
            .OrderByDescending(File.GetLastAccessTime)
            .Skip(maxEntries))
        {
            var pair = FilePair.FromContentFile(file);
            pair.PurgeItem();
        }
    }
}