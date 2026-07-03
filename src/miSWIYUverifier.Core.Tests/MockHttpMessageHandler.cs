using System.Net;
using System.Text;

namespace miSWIYUverifier.Core.Tests;

/// <summary>
/// Test double for HttpClient: returns queued responses in order and
/// records every request (method, URL, body) for assertions.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = new();

    public void Enqueue(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content != null)
            body = await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add((request.Method, request.RequestUri!.ToString(), body));

        var (status, responseBody) = _responses.Count > 0
            ? _responses.Dequeue()
            : (HttpStatusCode.NotFound, "{}");

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
    }
}
