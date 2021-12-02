using System.Security.Cryptography;

static class Hash
{
    public static string Compute(string value)
    {
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}