using System.Net;
using System.Net.Http;

namespace Mailozaurr;

internal sealed class GmailSubmissionException(HttpStatusCode statusCode, string responseContent)
    : HttpRequestException($"Gmail returned {(int)statusCode} ({statusCode}): {responseContent}") {
    internal HttpStatusCode SubmissionStatus { get; } = statusCode;
}
