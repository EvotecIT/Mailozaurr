using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    internal static GraphApiClient CreateSingleSendClient(OAuthCredential credential) {
        // Redirecting a POST can replay the message and its attachments to another origin.
        var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        return new GraphApiClient(http, credential: credential, ownsHttpClient: true);
    }

    internal async Task<HttpStatusCode> SendMimeMessageAsync(
        MimeKit.MimeMessage message, string userId, CancellationToken cancellationToken) {
        ThrowIfDisposed();
        using var mime = new MemoryStream();
        await message.WriteToAsync(mime, cancellationToken).ConfigureAwait(false);
        string content = Convert.ToBase64String(mime.GetBuffer(), 0, checked((int)mime.Length));
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUserSegment(userId) + "/sendMail") {
            Content = new StringContent(content, Encoding.ASCII, "text/plain")
        };
        ApplyAuthHeader(request);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }
}
