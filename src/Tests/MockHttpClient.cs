public class MockHttpClient :
    HttpClient
{
    IEnumerator responses;

    public MockHttpClient(params HttpResponseMessage[] responses) =>
        this.responses = responses.GetEnumerator();

    public override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Cancellation cancellation)
    {
        responses.MoveNext();
        return Task.FromResult((HttpResponseMessage) responses.Current!);
    }
}