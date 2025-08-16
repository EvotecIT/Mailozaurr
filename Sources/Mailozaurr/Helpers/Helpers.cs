using System.Net;
using System;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Linq;

namespace Mailozaurr;

/// <summary>
/// Utility methods used throughout the library.
/// </summary>
public static class Helpers {
    /// <summary>Converts a credential into an OAuth token tuple.</summary>
    /// <param name="credential">The credential containing the token.</param>
    /// <returns>The username and token.</returns>
    public static (string UserName, string Token) ConvertFromOAuth2Credential(NetworkCredential credential) {
        if (credential is null) {
            throw new ArgumentNullException(nameof(credential));
        }
        return (credential.UserName, credential.Password);
    }

    /// <summary>Creates a <see cref="NetworkCredential"/> from plain text.</summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <returns>The resulting credential.</returns>
    public static NetworkCredential ConvertFromPlainText(string userName, string password) {
        var secStringPassword = new SecureString();
        foreach (char c in password) {
            secStringPassword.AppendChar(c);
        }
        secStringPassword.MakeReadOnly();
        return new NetworkCredential(userName, secStringPassword);
    }

    /// <summary>Extracts the API key from a credential object.</summary>
    /// <param name="credentials">Credential containing the key.</param>
    /// <returns>The API key.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="credentials"/> is not a <see cref="NetworkCredential"/>.
    /// </exception>
    public static string CredentialToApiKey(ICredentials credentials) {
        if (credentials is NetworkCredential networkCredential) {
            return networkCredential.Password;
        }
        throw new ArgumentException("Credential must be of type NetworkCredential", nameof(credentials));
    }

    /// <summary>Retrieves the email address string from various types of objects.</summary>
    /// <param name="from">String or dictionary representation.</param>
    /// <returns>The email address.</returns>
    public static string GetEmailAddress(object from) {
        if (from is string s) {
            return s;
        }
        if (from is IDictionary<string, object> dict) {
            if (dict.TryGetValue("Email", out var emailObj)) {
                return emailObj?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }
        return from?.ToString() ?? string.Empty;
    }

    /// <summary>Creates an object representing the sender.</summary>
    /// <param name="email">Email address.</param>
    /// <param name="name">Display name.</param>
    /// <returns>The object to be used as sender.</returns>
    public static object GetFromObject(string email, string name) {
        if (!string.IsNullOrWhiteSpace(name)) {
            return new Dictionary<string, object> { { "Name", name }, { "Email", email } };
        }
        return email;
    }

    /// <summary>Parses an object into an email and optional name.</summary>
    /// <param name="from">String or dictionary representation.</param>
    /// <returns>Tuple containing the email and name.</returns>
    public static (string Email, string? Name) GetEmailAndName(object from) {
        if (from is string s) {
            return (s, null);
        }
        if (from is IDictionary dict) {
            var email = dict.Contains("Email") ? dict["Email"]?.ToString() : null;
            var name = dict.Contains("Name") ? dict["Name"]?.ToString() : null;
            return (email, name);
        }
        return (from?.ToString(), null);
    }

    /// <summary>
    /// Enumerates unique address objects based on email value using the provided hash set to track seen addresses.
    /// </summary>
    /// <param name="addresses">Collection of address objects.</param>
    /// <param name="seen">Hash set tracking emails that were already yielded.</param>
    /// <returns>Unique address objects.</returns>
    public static IEnumerable<object> UniqueAddresses(IEnumerable<object>? addresses, HashSet<string> seen) {
        if (addresses == null) yield break;

        foreach (var address in addresses) {
            var email = GetEmailAddress(address);
            if (string.IsNullOrWhiteSpace(email)) {
                continue;
            }

            var normalized = string
                .Concat(email.Where(c => !char.IsWhiteSpace(c)))
                .ToLowerInvariant();
            if (seen.Add(normalized)) {
                yield return address;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified exception represents a transient error
    /// that can be retried safely.
    /// </summary>
    /// <param name="ex">The exception to inspect.</param>
    /// <returns><c>true</c> if the error is transient; otherwise <c>false</c>.</returns>
    public static bool IsTransient(Exception ex) {
        switch (ex) {
            case HttpRequestException httpEx:
                // HttpRequestException.StatusCode was introduced in .NET 5.0
#if NET5_0_OR_GREATER
                if (httpEx.StatusCode.HasValue) {
                    var code = (int)httpEx.StatusCode.Value;
                    return code >= 500 || code == 408 || code == 429;
                }
#endif
                return true;
            case SmtpCommandException smtpEx:
                var smtpCode = (int)smtpEx.StatusCode;
                return smtpCode >= 400 && smtpCode < 500;
            case SmtpProtocolException:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Posts the provided <see cref="SmtpResult"/> to a webhook endpoint.
    /// </summary>
    /// <param name="url">Destination webhook URL.</param>
    /// <param name="result">Result object describing the send operation.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="client">Optional HTTP client to reuse.</param>
    public static async Task PostWebhookAsync(string? url, SmtpResult result, CancellationToken cancellationToken = default, HttpClient? client = null) {
        if (string.IsNullOrWhiteSpace(url)) {
            return;
        }

        HttpClient? ownedClient = null;

        try {
            if (client == null) {
                ownedClient = new HttpClient();
                client = ownedClient;
            }
            var json = JsonSerializer.Serialize(result);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) {
                LoggingMessages.Logger.WriteWarning(
                    $"Failed to post webhook: {(int)response.StatusCode} {response.ReasonPhrase}");
            }
        } catch (HttpRequestException ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to post webhook: {ex.Message}");
        } finally {
            ownedClient?.Dispose();
        }
    }
}
