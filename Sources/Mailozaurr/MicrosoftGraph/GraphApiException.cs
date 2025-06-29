using System;
using System.Net;

namespace Mailozaurr;

/// <summary>
/// Exception thrown when Microsoft Graph API returns an error.
/// </summary>
public class GraphApiException : Exception {
    /// <summary>
    /// HTTP status code returned by the Graph API.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Raw response content returned by the Graph API.
    /// </summary>
    public string ResponseContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="responseContent">Raw response content from the server.</param>
    public GraphApiException(HttpStatusCode statusCode, string message, string responseContent)
        : base(message) {
        StatusCode = statusCode;
        ResponseContent = responseContent;
    }
}
