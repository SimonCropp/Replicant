static class Retry
{
    public static bool IsRetryableStatus(HttpStatusCode status) =>
        status is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    public static bool IsRetryableException(Exception exception)
    {
        if (exception is TaskCanceledException)
        {
            return false;
        }

        for (var inner = exception; inner != null; inner = inner.InnerException)
        {
            if (inner is SocketException se)
            {
                return se.SocketErrorCode is
                    SocketError.ConnectionReset or
                    SocketError.ConnectionAborted or
                    SocketError.Shutdown or
                    SocketError.TimedOut or
                    SocketError.TryAgain;
            }
        }

        return exception is HttpRequestException;
    }

    public static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> sendAsync,
        int maxRetries,
        Cancel cancel)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await sendAsync();
            }
            catch (Exception exception) when
                (attempt < maxRetries && IsRetryableException(exception))
            {
                await Task.Delay(GetDelay(attempt), cancel);
                continue;
            }

            if (attempt < maxRetries &&
                IsRetryableStatus(response.StatusCode))
            {
                response.Dispose();
                await Task.Delay(GetDelay(attempt), cancel);
                continue;
            }

            return response;
        }
    }

    public static HttpResponseMessage Send(
        Func<HttpResponseMessage> send,
        int maxRetries)
    {
        for (var attempt = 0; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = send();
            }
            catch (Exception exception) when
                (attempt < maxRetries && IsRetryableException(exception))
            {
                Thread.Sleep(GetDelay(attempt));
                continue;
            }

            if (attempt < maxRetries &&
                IsRetryableStatus(response.StatusCode))
            {
                response.Dispose();
                Thread.Sleep(GetDelay(attempt));
                continue;
            }

            return response;
        }
    }

    static TimeSpan GetDelay(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Pow(2, attempt + 1) * 100);
}
