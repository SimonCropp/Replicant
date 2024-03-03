public class StoredData(
    DateTimeOffset? expiry,
    DateTimeOffset? modified,
    string? etag)
{
    public DateTimeOffset? Expiry { get; } = expiry;
    public DateTimeOffset? Modified { get; } = modified;
    public string? Etag { get; } = etag;

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Expiry == null)
        {
            builder.Append("exp=null_");
        }
        else
        {
            builder.Append($"exp={Expiry:yyyyMMdd}_");
        }

        if (Modified == null)
        {
            builder.Append("mod=null_");
        }
        else
        {
            builder.Append($"mod={Modified:yyyyMMdd}_");
        }

        if (Etag == null)
        {
            builder.Append("tag=null_");
        }
        else
        {
            builder.Append($"tag={Etag}_");
        }

        return builder.ToString().Trim('_');
    }
}