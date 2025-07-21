using System;
using System.Net;

namespace Mailozaurr;

/// <summary>
/// Exception thrown when Gmail API authentication fails.
/// </summary>
public class GmailAuthenticationException : Exception
{
    /// <summary>
    /// HTTP status code returned by the Gmail API.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Raw response content returned by the Gmail API.
    /// </summary>
    public string ResponseContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailAuthenticationException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseContent">Raw response content from the server.</param>
    public GmailAuthenticationException(HttpStatusCode statusCode, string responseContent)
        : base($"Gmail API authentication failed with status {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
