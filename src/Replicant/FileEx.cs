using System.IO;

static class FileEx
{
    public static FileStream OpenRead(string path)
    {
        return new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }
}