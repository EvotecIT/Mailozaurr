using MailKit.Security;

namespace Mailozaurr;

/// <summary>An SMTP host and its optional transport security override.</summary>
public sealed class SmtpEndpoint {
    /// <summary>DNS name or IP address without a URI scheme.</summary>
    public string Server { get; set; } = string.Empty;
    /// <summary>TCP port, defaulting to submission port 587.</summary>
    public int Port { get; set; } = 587;
    /// <summary>Overrides the request's effective security options for this endpoint.</summary>
    public SecureSocketOptions? SecureSocketOptions { get; set; }

    /// <summary>Parses a bare host or an smtp, smtps or smtp+starttls URI. Credentials and URI suffixes are rejected.</summary>
    public static SmtpEndpoint Parse(string hostOrUri, int port, SecureSocketOptions defaults) {
        if (string.IsNullOrWhiteSpace(hostOrUri)) throw new ArgumentException("An SMTP host is required.", nameof(hostOrUri));
        var text = hostOrUri.Trim();
        var security = defaults;
        string host;
        if (text.IndexOf("://", StringComparison.Ordinal) >= 0) {
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || (uri.AbsolutePath != "" && uri.AbsolutePath != "/"))
                throw new ArgumentException("SMTP URI must contain only a host and optional port.", nameof(hostOrUri));
            security = uri.Scheme.ToLowerInvariant() switch {
                "smtp" => MailKit.Security.SecureSocketOptions.None,
                "smtps" => MailKit.Security.SecureSocketOptions.SslOnConnect,
                "smtp+starttls" => MailKit.Security.SecureSocketOptions.StartTls,
                _ => throw new ArgumentException("Unsupported SMTP URI scheme.", nameof(hostOrUri))
            };
            // Reject even an explicit trailing slash; endpoints are authorities, not resources.
            var authority = text.Substring(text.IndexOf("://", StringComparison.Ordinal) + 3);
            if (authority.IndexOfAny(new[] { '/', '?', '#', '@', '\\' }) >= 0)
                throw new ArgumentException("SMTP URI must not contain credentials or a path.", nameof(hostOrUri));
            host = uri.Host.Trim('[', ']');
            if (uri.Port > 0) port = uri.Port;
        } else {
            host = text.Trim('[', ']');
        }
        if (Uri.CheckHostName(host) == UriHostNameType.Unknown || host.IndexOfAny(new[] { '/', '\\', '@', '?', '#' }) >= 0)
            throw new ArgumentException("Invalid SMTP host.", nameof(hostOrUri));
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (!Enum.IsDefined(typeof(SecureSocketOptions), security)) throw new ArgumentOutOfRangeException(nameof(defaults));
        return new SmtpEndpoint { Server = host, Port = port, SecureSocketOptions = security };
    }
}
