// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
public class HttpResponseMessageEx(HttpStatusCode code) :
    HttpResponseMessage(code)
{
    protected override void Dispose(bool disposing)
    {
    }

    public override string ToString()
    {
        var builder = new StringBuilder($"{StatusCode}_");
        var cacheControl = Headers.CacheControl;
        if (cacheControl == null)
        {
            builder.Append("cache=null_");
        }
        else
        {
            builder.Append($"cache={cacheControl.ToString().Replace(", ", ",")}_");
        }

        var webExpires = Content?.Headers.Expires;
        if (webExpires == null)
        {
            builder.Append("exp=null_");
        }
        else
        {
            builder.Append($"exp={webExpires.Value:yyyyMMdd}_");
        }

        var webMod = Content?.Headers.LastModified;
        if (webMod == null)
        {
            builder.Append("mod=null_");
        }
        else
        {
            builder.Append($"mod={webMod.Value:yyyyMMdd}_");
        }

        var webEtag = Headers.ETag;
        if (webEtag == null)
        {
            builder.Append("mod=null_");
        }
        else
        {
            var etag = Etag.FromResponse(this);
            if (etag.IsEmpty)
            {
                builder.Append("mod=empty_");
            }
            else
            {
                builder.Append($"mod={etag.ForFile}_");
            }
        }

        return builder.ToString().Trim('_');
    }
}