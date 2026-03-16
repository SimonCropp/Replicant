using System.Net.Http;

class MockHttpMessageHandler :
    HttpMessageHandler
{
    HttpResponseMessage response;
    byte[] responseBytes = "benchmark-response-content"u8.ToArray();

    public MockHttpMessageHandler(HttpResponseMessage response) =>
        this.response = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancel) =>
        Task.FromResult(CloneResponse());

    protected override HttpResponseMessage Send(
        HttpRequestMessage request, CancellationToken cancel) =>
        CloneResponse();

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
}
