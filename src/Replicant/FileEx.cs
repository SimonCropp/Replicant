static class FileEx
{
    //Seems windows or .net behaviour has changed and using FromFileTimeUtc(0) is now ignored
    public static DateTime OldMinFileDate { get; } = DateTime.FromFileTimeUtc(0);
    public static DateTime MinFileDate { get; }  = DateTime.FromFileTimeUtc(1);

    public static Encoding Default(this Encoding? encoding) =>
        encoding ?? Encoding.UTF8;

    public static Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

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

    public static Stream OpenWrite(string path) =>
        new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

#if NET7_0_OR_GREATER

    public static void Move(string source, string target) =>
        File.Move(source, target, true);

    public static Task<byte[]> ReadAllBytesAsync(string path, Cancel cancel) =>
        File.ReadAllBytesAsync(path, cancel);

    public static Task<string> ReadAllTextAsync(string path, Cancel cancel) =>
        File.ReadAllTextAsync(path, cancel);

#else

    public static void Move(string source, string target)
    {
        File.Delete(target);
        File.Move(source, target);
    }

    public static Task<byte[]> ReadAllBytesAsync(string path, Cancel cancel) =>
        Task.FromResult(File.ReadAllBytes(path));

    public static Task<string> ReadAllTextAsync(string path, Cancel cancel) =>
        Task.FromResult(File.ReadAllText(path));

#endif
}