// ReSharper disable StringLiteralTypo
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EmailValidation;

namespace Mailozaurr;

public static class Validator {
    private static readonly HashSet<string> DisposableDomains;
    private static readonly HashSet<string> AllowedDomains;

    static Validator() {
        DisposableDomains = LoadDomainsFromResource("disposable_email_blocklist.conf");
        AllowedDomains = LoadDomainsFromResource("allowlist.conf");
    }

    private static HashSet<string> LoadDomainsFromResource(string resourceName) {
        var assembly = typeof(Validator).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Mailozaurr.Resources.{resourceName}");
        if (stream == null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = new StreamReader(stream);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) != null) {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            set.Add(line);
        }
        return set;
    }
    private static bool IsDisposableEmail(string email) {
        var domain = email.Split('@').Last();
        return IsDisposableDomain(domain);
    }

    /// <summary>
    /// Determines whether [is disposable domain] [the specified domain].
    /// </summary>
    /// <param name="domain">The domain.</param>
    /// <returns>
    ///   <c>true</c> if [is disposable domain] [the specified domain]; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsDisposableDomain(string domain) {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        if (AllowedDomains.Contains(normalizedDomain)) {
            return false;
        }
        return DisposableDomains.Contains(normalizedDomain);
    }

    /// <summary>
    /// Validates the email. This method will validate the email address and check if it is a disposable email address.
    /// </summary>
    /// <param name="emailAddress">The email address.</param>
    /// <param name="allowInternational">if set to <c>true</c> [allow international].</param>
    /// <param name="allowTopLevelDomains">if set to <c>true</c> [allow top level domains].</param>
    /// <returns></returns>
    public static ValidatedEmail ValidateEmail(string emailAddress, bool allowInternational = false, bool allowTopLevelDomains = false) {
        bool isDisposable = false;
        try {
            var isValid = EmailValidator.TryValidate(emailAddress, allowTopLevelDomains, allowInternational, out EmailValidationError errorReason);
            if (isValid) {
                isDisposable = IsDisposableEmail(emailAddress);
            }
            return new ValidatedEmail {
                EmailAddress = emailAddress,
                IsValid = isValid,
                IsDisposable = isDisposable,
                Reason = errorReason.Code,
                ReasonTokenIndex = errorReason.TokenIndex,
                ReasonErrorIndex = errorReason.ErrorIndex,
                Error = ""
            };
        } catch (FormatException ex) {
            return new ValidatedEmail {
                EmailAddress = emailAddress,
                IsValid = false,
                IsDisposable = false,
                Reason = EmailValidationError.None.Code,
                Error = ex.Message
            };
        }
    }
}
