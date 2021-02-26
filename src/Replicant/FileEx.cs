using System.IO;

static class FileEx
{
    public static FileStream OpenRead(string path)
    {
        return new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
    }
    public static FileStream OpenWrite(string path)
    {
        return new(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
    }
}