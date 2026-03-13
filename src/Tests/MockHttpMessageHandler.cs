class MockHttpMessageHandler(params HttpResponseMessage[] responses) :
    HttpMessageHandler
{
    IEnumerator responses = responses.GetEnumerator();

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, Cancel cancel)
    {
        responses.MoveNext();
        return Task.FromResult((HttpResponseMessage) responses.Current!);
    }
}