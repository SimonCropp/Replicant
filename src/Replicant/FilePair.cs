using System;
using System.Diagnostics;
using System.IO;
using Replicant;

[DebuggerDisplay("Content = {Content} | Meta = {Meta}")]
readonly struct FilePair
{
    public string Content { get; }
    public string Meta { get; }

    public FilePair(string content, string meta)
    {
        Content = content;
        Meta = meta;
    }

    public static FilePair FromContentFile(string path)
    {
        return new(path, Path.ChangeExtension(path, "json"));
    }

    public bool Exists()
    {
        return File.Exists(Content) &&
               File.Exists(Meta);
    }

    public static FilePair FromContentFile(FileInfo path)
    {
        return FromContentFile(path.FullName);
    }
    public void Delete()
    {
        File.Delete(Content);
        File.Delete(Meta);
    }

    public void SetExpiry(DateTimeOffset? expiry)
    {
        if (expiry == null)
        {
            File.SetLastWriteTimeUtc(Content, FileEx.MinFileDate);
        }
        else
        {
            File.SetLastWriteTimeUtc(Content, expiry.Value.UtcDateTime);
        }
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

    public FilePair ToTemp()
    {
        var tempContent = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        return new(tempContent, tempMeta);
    }

    public static FilePair GetTemp()
    {
        var tempContent = FileEx.GetTempFileName();
        var tempMeta = FileEx.GetTempFileName();
        return new(tempContent, tempMeta);
    }


}