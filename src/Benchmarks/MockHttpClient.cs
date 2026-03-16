using System.Collections;
using System.Net.Http;

class MockHttpClient :
    HttpClient
{
    HttpResponseMessage response;

    public MockHttpClient(HttpResponseMessage response) =>
        this.response = response;

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
        Task.FromResult(CloneResponse());

    HttpResponseMessage CloneResponse()
    {
        var clone = new HttpResponseMessage(response.StatusCode)
        {
            Content = new ByteArrayContent(responseBytes)
        };

        clone.Headers.CacheControl = response.Headers.CacheControl;

        if (response.Content.Headers.LastModified != null)
        {
            clone.Content.Headers.LastModified = response.Content.Headers.LastModified;
        }

        if (response.Headers.ETag != null)
        {
            clone.Headers.ETag = response.Headers.ETag;
        }

        return clone;
    }

    byte[] responseBytes = "benchmark-response-content"u8.ToArray();
}
