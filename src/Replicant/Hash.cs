using System.Security.Cryptography;

static class Hash
{
    public static string Compute(string value)
    {
#if NET7_0_OR_GREATER
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(value));
#else
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
#endif
        return Convert.ToHexStringLower(hash);
    }
}