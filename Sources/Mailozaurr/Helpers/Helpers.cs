﻿using System.Net;
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
}
