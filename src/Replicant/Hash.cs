using System.Linq;
using System.Security.Cryptography;
using System.Text;

static class Hash
{
    public static string Compute(string value)
    {
        using SHA1Managed sha = new();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}