using System;

namespace Mailozaurr;

/// <summary>
/// Exception thrown when Gmail API returns an unexpected response.
/// </summary>
public class GmailApiException : Exception {
    /// <summary>
    /// Raw response content returned by the Gmail API.
    /// </summary>
    public string ResponseContent { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GmailApiException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="responseContent">Raw response content from the server.</param>
    /// <param name="innerException">The original exception.</param>
    public GmailApiException(string message, string responseContent, Exception innerException)
        : base(message, innerException) {
        ResponseContent = responseContent;
    }
}

