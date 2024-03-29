public class MockHttpClient :
    HttpClient
{
    IEnumerator responses;

    public MockHttpClient(params HttpResponseMessage[] responses) =>
        this.responses = responses.GetEnumerator();

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Cancel cancel)
    {
        responses.MoveNext();
        return Task.FromResult((HttpResponseMessage) responses.Current!);
    }
}