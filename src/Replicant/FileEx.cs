using System;
using System.IO;

static class FileEx
{
    public static DateTime MinFileDate { get; } = DateTime.FromFileTimeUtc(0);
    public static DateTimeOffset MinFileDateTimeOffset { get; } = new(MinFileDate);
    public static DateTime MaxFileDate { get; } = new(2107,12,31);

    public static FileStream OpenRead(string path)
    {
        return new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
    }

    public static string GetTempFileName(string? extension = null)
    {
        var tempPath = Path.GetTempPath();
        var randomFileName = Path.GetRandomFileName();
        if (extension != null)
        {
            randomFileName = $"{randomFileName}.{extension}";
        }

        return Path.Combine(tempPath, randomFileName);
    }

    public static FileStream OpenWrite(string path)
    {
        return new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
    }
}