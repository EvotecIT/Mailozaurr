namespace Mailozaurr;

/// <summary>Represents a sanitized JMAP protocol or method error.</summary>
public sealed class JmapApiException : Exception {
    /// <summary>Creates a JMAP API exception.</summary>
    public JmapApiException(string errorType, string message)
        : base(message) {
        ErrorType = string.IsNullOrWhiteSpace(errorType) ? "unknown" : errorType.Trim();
    }

    /// <summary>JMAP method error type, when supplied by the server.</summary>
    public string ErrorType { get; }
}
