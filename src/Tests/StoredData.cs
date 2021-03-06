using System;
using System.Text;

public class StoredData
{
    public DateTimeOffset? Expiry { get; }
    public DateTimeOffset? Modified { get; }
    public string? Etag { get; }

    public StoredData(
        DateTimeOffset? expiry,
        DateTimeOffset? modified,
        string? etag)
    {
        Expiry = expiry;
        Modified = modified;
        Etag = etag;
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        if (Expiry == null)
        {
            builder.Append("expiry=null_");
        }
        else
        {
            builder.Append($"expiry={Expiry:yyyyMMdd}_");
        }

        if (Modified == null)
        {
            builder.Append("modified=null_");
        }
        else
        {
            builder.Append($"modified={Modified:yyyyMMdd}_");
        }

        if (Etag == null)
        {
            builder.Append("etag=null_");
        }
        else
        {
            builder.Append($"etag={Etag}_");
        }

        return builder.ToString().Trim('_');
    }
}