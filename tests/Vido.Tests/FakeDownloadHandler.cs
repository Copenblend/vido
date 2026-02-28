namespace Vido.Tests;

/// <summary>
/// Test-only <see cref="HttpMessageHandler"/> that returns fixed binary payload content.
/// </summary>
internal sealed class FakeDownloadHandler : HttpMessageHandler
{
    private readonly byte[] _content;

    /// <summary>
    /// Initializes a new fake download handler with the provided payload bytes.
    /// </summary>
    /// <param name="content">Binary payload to emit as the response body.</param>
    public FakeDownloadHandler(byte[] content)
    {
        _content = content;
    }

    /// <summary>
    /// Sends a fake HTTP response containing the configured payload.
    /// </summary>
    /// <param name="request">Incoming request (unused by this fake).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A completed task containing an HTTP 200 response with payload content.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_content)
        };
        response.Content.Headers.ContentLength = _content.Length;
        return Task.FromResult(response);
    }
}
