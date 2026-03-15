static class Hash
{
    public static string Compute(string value)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }
}