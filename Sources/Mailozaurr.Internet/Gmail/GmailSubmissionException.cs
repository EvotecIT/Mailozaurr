using System.Net;
using System.Net.Http;

namespace Mailozaurr;

internal sealed class GmailSubmissionException(HttpStatusCode statusCode)
    : HttpRequestException("Gmail rejected the submission.") {
    internal HttpStatusCode SubmissionStatus { get; } = statusCode;
}
