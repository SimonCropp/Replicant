using Newtonsoft.Json;
using VerifyTests;

class TimestampConverter :
    WriteOnlyJsonConverter<Timestamp>
{
    public override void WriteJson(
        JsonWriter writer,
        Timestamp timestamp,
        JsonSerializer serializer,
        IReadOnlyDictionary<string, object> context)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("MetaFileName");
        writer.WriteValue(timestamp.MetaFileName);
        writer.WritePropertyName("ContentFileName");
        writer.WriteValue(timestamp.ContentFileName);
        writer.WritePropertyName("Prefix");
        writer.WriteValue(timestamp.Prefix);
        writer.WritePropertyName("Modified");
        writer.WriteValue(timestamp.Modified);
        writer.WritePropertyName("UriHash");
        writer.WriteValue(timestamp.UriHash);
        if (timestamp.Etag.IsEmpty)
        {
            writer.WritePropertyName("Etag");
            writer.WriteValue("Empty");
        }
        else
        {
            writer.WritePropertyName("EtagForFile");
            writer.WriteValue(timestamp.Etag.ForFile);
            writer.WritePropertyName("EtagForWeb");
            writer.WriteValue(timestamp.Etag.ForWeb);
        }
        writer.WriteEndObject();
    }
}