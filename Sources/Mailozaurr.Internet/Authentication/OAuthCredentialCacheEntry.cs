using System;

namespace Mailozaurr;

#pragma warning disable CS1591
public sealed class OAuthCredentialCacheEntry {
    public string UserName { get; set; } = string.Empty;

    public string? AccessTokenProtected { get; set; }

    public string? AccessToken { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }

    public string? RefreshTokenProtected { get; set; }

    public string? RefreshToken { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecretProtected { get; set; }

    public string? ClientSecret { get; set; }

    public string? ServiceAccountJsonProtected { get; set; }

    public string? ServiceAccountJson { get; set; }

    public string? ServiceAccountSubject { get; set; }

    public static OAuthCredentialCacheEntry FromCredential(OAuthCredential credential, ICredentialProtector protector) {
        if (credential == null) {
            throw new ArgumentNullException(nameof(credential));
        }

        if (protector == null) {
            throw new ArgumentNullException(nameof(protector));
        }

        return new OAuthCredentialCacheEntry {
            UserName = credential.UserName,
            AccessTokenProtected = ProtectRequired(protector, credential.AccessToken),
            ExpiresOn = credential.ExpiresOn,
            RefreshTokenProtected = ProtectOptional(protector, credential.RefreshToken),
            ClientId = credential.ClientId,
            ClientSecretProtected = ProtectOptional(protector, credential.ClientSecret),
            ServiceAccountJsonProtected = ProtectOptional(protector, credential.ServiceAccountJson),
            ServiceAccountSubject = credential.ServiceAccountSubject
        };
    }

    public OAuthCredential ToCredential(ICredentialProtector protector) {
        if (protector == null) {
            throw new ArgumentNullException(nameof(protector));
        }

        return new OAuthCredential {
            UserName = UserName,
            AccessToken = ReadRequiredSecret(protector, AccessTokenProtected, AccessToken),
            ExpiresOn = ExpiresOn,
            RefreshToken = ReadOptionalSecret(protector, RefreshTokenProtected, RefreshToken),
            ClientId = ClientId,
            ClientSecret = ReadOptionalSecret(protector, ClientSecretProtected, ClientSecret),
            ServiceAccountJson = ReadOptionalSecret(protector, ServiceAccountJsonProtected, ServiceAccountJson),
            ServiceAccountSubject = ServiceAccountSubject
        };
    }

    private static string ProtectRequired(ICredentialProtector protector, string value) {
        if (string.IsNullOrEmpty(value)) {
            return string.Empty;
        }

        return protector.Protect(value);
    }

    private static string? ProtectOptional(ICredentialProtector protector, string? value) {
        if (string.IsNullOrEmpty(value)) {
            return null;
        }

        return protector.Protect(value!);
    }

    private static string ReadRequiredSecret(ICredentialProtector protector, string? protectedValue, string? legacyValue) {
        var value = ReadOptionalSecret(protector, protectedValue, legacyValue);
        return value ?? string.Empty;
    }

    private static string? ReadOptionalSecret(ICredentialProtector protector, string? protectedValue, string? legacyValue) {
        if (!string.IsNullOrEmpty(protectedValue)) {
            var unprotected = CredentialProtection.UnprotectWithFallback(protector, protectedValue);
            if (!string.IsNullOrEmpty(unprotected) || string.IsNullOrEmpty(legacyValue)) {
                return unprotected;
            }
        }

        return legacyValue;
    }
}
#pragma warning restore CS1591