using Replicant;

[DebuggerDisplay("Content = {Content} | Meta = {Meta}")]
readonly struct FilePair(string content, string meta)
{
    public string Content { get; } = content;
    public string Meta { get; } = meta;

    public static FilePair FromContentFile(string path) =>
        new(path, Path.ChangeExtension(path, "json"));

    public bool Exists() =>
        File.Exists(Content) &&
        File.Exists(Meta);

    public static FilePair FromContentFile(FileInfo path) =>
        FromContentFile(path.FullName);

    public void Delete()
    {
        File.Delete(Content);
        File.Delete(Meta);
    }

    public void SetExpiry(DateTimeOffset? expiry)
    {
        var expiryDate = expiry?.UtcDateTime ?? FileEx.MinFileDate;
        File.SetLastWriteTimeUtc(Content, expiryDate >= FileEx.MinFileDate ? expiryDate : FileEx.MinFileDate);
    }

    public void PurgeItem()
    {
        var tempContent = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        var metaPath = Path.ChangeExtension(Content, "json");
        try
        {
            File.Move(Content, tempContent);
            File.Move(metaPath, tempMeta);
        }
        catch (Exception)
        {
            try
            {
                TryMoveTempFilesBack(Content, tempContent, tempMeta, metaPath);

               HttpCache.LogError($"Could not purge item due to locked file. Cached item remains. Path: {Content}");
            }
            catch (Exception e)
            {
                throw new($"Could not purge item due to locked file. Cached item is in a corrupted state. Path: {Content}", e);
            }
        }
        finally
        {
            File.Delete(tempContent);
            File.Delete(tempMeta);
        }
    }

    static void TryMoveTempFilesBack(string contentPath, string tempContent, string tempMeta, string metaPath)
    {
        if (File.Exists(tempContent))
        {
            FileEx.Move(tempContent, contentPath);
        }

        if (File.Exists(tempMeta))
        {
            FileEx.Move(tempMeta, metaPath);
        }
    }

    public static FilePair GetTemp()
    {
        var tempContent = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        return new(tempContent, tempMeta);
    }
}