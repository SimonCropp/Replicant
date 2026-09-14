static class FileEx
{
    //Seems windows or .net behaviour has changed and using FromFileTimeUtc(0) is now ignored
    public static DateTime OldMinFileDate { get; } = DateTime.FromFileTimeUtc(0);
    // Written as the expiry of an entry that has no expiry information
    public static DateTime MinFileDate { get; }  = DateTime.FromFileTimeUtc(1);

    // Written as the expiry of an entry that is already expired but whose expiry can't be stored as a file time,
    // e.g. no-cache or an invalid Expires such as "-1". Kept distinct from MinFileDate.
    public static DateTime ExpiredFileDate { get; } = new(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    public static string TempPath { get; } = Path.GetTempPath();

    public static Encoding Default(this Encoding? encoding) =>
        encoding ?? Encoding.UTF8;

    public static Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

    public static string GetTempFileName(string? extension = null)
    {
        var randomFileName = Path.GetRandomFileName();
        if (extension != null)
        {
            randomFileName = $"{randomFileName}.{extension}";
        }

        return Path.Combine(TempPath, randomFileName);
    }

    public static Stream OpenWrite(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
}