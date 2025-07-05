using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public class RecordingHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    private readonly Queue<HttpResponseMessage> _responses;

    public RecordingHandler(params HttpResponseMessage[] responses)
    {
        _responses = new Queue<HttpResponseMessage>(responses);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpRequestMessage copy = new(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (request.Content != null)
        {
            byte[] bytes = await request.Content.ReadAsByteArrayAsync();
            copy.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        Requests.Add(copy);
        HttpResponseMessage response = _responses.Count > 0 ? _responses.Dequeue() : new HttpResponseMessage(HttpStatusCode.OK);
        return response;
    }
}
