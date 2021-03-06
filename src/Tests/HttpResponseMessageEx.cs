using System.Net;
using System.Net.Http;
using System.Text;

public class HttpResponseMessageEx :
    HttpResponseMessage
{
    public HttpResponseMessageEx(HttpStatusCode code) :
        base(code)
    {
    }

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
            builder.Append("expires=null_");
        }
        else
        {
            builder.Append($"expires={webExpires.Value:yyyyMMdd}_");
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
                builder.Append($"mod={etag.ForFile.Substring(1)}_");
            }
        }

        return builder.ToString().Trim('_');
    }
}