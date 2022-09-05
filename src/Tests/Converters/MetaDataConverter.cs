class MetaDataConverter :
    WriteOnlyJsonConverter<MetaData>
{
    public override void Write(VerifyJsonWriter writer, MetaData result) =>
        writer.Serializer.Serialize(
            writer,
            new
            {
                result.Uri,
                ResponseHeaders = result.ResponseHeaders.ToDictionary(_ => _.Key, _ => _.Value),
                ContentHeaders = result.ContentHeaders.ToDictionary(_ => _.Key, _ => _.Value),
                TrailingHeaders = result.TrailingHeaders?.ToDictionary(_ => _.Key, _ => _.Value)
            });
}