using System.Net;
using System.Security;

namespace Mailozaurr;

public static class Helpers {
    public static (string UserName, string Token) ConvertFromOAuth2Credential(NetworkCredential credential) {
        return (credential.UserName, credential.Password);
    }

    public static NetworkCredential ConvertFromPlainText(string userName, string password) {
        var secStringPassword = new SecureString();
        foreach (char c in password) {
            secStringPassword.AppendChar(c);
        }
        return new NetworkCredential(userName, secStringPassword);
    }

    public static string CredentialToApiKey(ICredentials credentials) {
        string apiKey;
        try {
            var networkCredential = credentials as NetworkCredential;
            apiKey = networkCredential.Password;
        } catch (Exception ex) {
            apiKey = "";
        }
        return apiKey;
    }

    public static string GetEmailAddress(object from) {
        if (from is string s) {
            return s;
        }
        if (from is IDictionary<string, object> dict && dict.ContainsKey("Email")) {
            return dict["Email"]?.ToString();
        }
        return from?.ToString() ?? string.Empty;
    }

    public static object GetFromObject(string email, string name) {
        if (!string.IsNullOrEmpty(name)) {
            return new Dictionary<string, object> { { "Name", name }, { "Email", email } };
        }
        return email;
    }

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
}
