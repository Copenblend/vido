namespace Vido.Tests;

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that returns fixed JSON content or throws a configured exception.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string? _responseContent;
    private readonly Exception? _exception;

    /// <summary>
    /// Initializes a successful fake response handler.
    /// </summary>
    /// <param name="responseContent">JSON content to return in the response body.</param>
    public FakeHttpMessageHandler(string responseContent)
    {
        _responseContent = responseContent;
    }

    /// <summary>
    /// Initializes a fake response handler that throws an exception.
    /// </summary>
    /// <param name="exception">Exception to throw from <see cref="SendAsync"/>.</param>
    public FakeHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    /// <summary>
    /// Sends a fake HTTP response for tests.
    /// </summary>
    /// <param name="request">Incoming request (unused by this fake).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A completed task containing the configured response.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exception is not null)
            throw _exception;

        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(_responseContent!, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
