static class Hash
{
    public static string Compute(string value)
    {
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(value.Length);
        if (maxByteCount <= 256)
        {
            Span<byte> utf8 = stackalloc byte[256];
            var written = Encoding.UTF8.GetBytes(value.AsSpan(), utf8);
            Span<byte> hash = stackalloc byte[20];
            SHA1.HashData(utf8[..written], hash);
            return Convert.ToHexStringLower(hash);
        }

        var rented = ArrayPool<byte>.Shared.Rent(maxByteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(value.AsSpan(), rented);
            Span<byte> hash = stackalloc byte[20];
            SHA1.HashData(rented.AsSpan(0, written), hash);
            return Convert.ToHexStringLower(hash);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}