namespace Mailozaurr;

/// <summary>
/// Common well-known secret names used by profile-based adapters.
/// </summary>
public static class MailSecretNames {
    /// <summary>Password for username/password authentication.</summary>
    public const string Password = "password";

    /// <summary>Client secret for confidential client auth flows.</summary>
    public const string ClientSecret = "clientSecret";

    /// <summary>Access token for temporary explicit sessions.</summary>
    public const string AccessToken = "accessToken";

    /// <summary>Refresh token when stored explicitly.</summary>
    public const string RefreshToken = "refreshToken";

    /// <summary>Certificate password when certificate auth is used.</summary>
    public const string CertificatePassword = "certificatePassword";

    /// <summary>API key used by an HTTP delivery provider.</summary>
    public const string ApiKey = "apiKey";

    /// <summary>Amazon access key identifier.</summary>
    public const string AccessKeyId = "accessKeyId";

    /// <summary>Amazon secret access key.</summary>
    public const string SecretAccessKey = "secretAccessKey";
}
