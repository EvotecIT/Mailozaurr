namespace Mailozaurr;

/// <summary>
/// Helper methods for validating SMTP connection and authentication settings.
/// </summary>
public static class SmtpValidation
{
    /// <summary>
    /// Validates the SMTP server and port values.
    /// </summary>
    public static bool TryValidateServer(string? server, int port, out string? error)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            error = "SMTP server is required.";
            return false;
        }

        if (port <= 0)
        {
            error = "SMTP port must be greater than 0.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates credentials intended for SMTP authentication.
    /// </summary>
    public static bool TryValidateCredentials(string? username, string? password, out string? error)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            error = "SMTP username is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            error = "SMTP password is required.";
            return false;
        }

        error = null;
        return true;
    }
}
