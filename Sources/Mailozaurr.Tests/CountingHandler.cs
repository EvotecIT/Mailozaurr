using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr.Tests;

public class CountingHandler : HttpMessageHandler
{
    private readonly TimeSpan _delay;
    private int _current;
    public int MaxConcurrency { get; private set; }

    public CountingHandler(TimeSpan delay)
    {
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var concurrency = Interlocked.Increment(ref _current);
        if (concurrency > MaxConcurrency)
        {
            MaxConcurrency = concurrency;
        }
        await Task.Delay(_delay, cancellationToken);
        Interlocked.Decrement(ref _current);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1 })
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        };
        return response;
    }
}
