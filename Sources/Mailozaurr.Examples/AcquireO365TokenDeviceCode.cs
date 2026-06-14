using Mailozaurr;
using System;
using System.Threading.Tasks;

/// <summary>
/// Obtains an OAuth token for Office 365 using the device code flow.
/// </summary>
public static class AcquireO365TokenDeviceCode {
    /// <summary>Runs the example.</summary>
    public static async Task RunAsync() {
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string[] scopes = { "Mail.ReadWrite", "Mail.Send" };

        try {
            var cred = await OAuthHelpers.AcquireO365TokenDeviceCodeAsync(
                clientId,
                tenantId,
                scopes,
                result => {
                    Console.WriteLine(result.Message);
                    return Task.CompletedTask;
                });
            Console.WriteLine($"Token acquired for {cred.UserName}: {cred.AccessToken.Substring(0, 5)}...");
        } catch (Exception ex) {
            Console.WriteLine($"AcquireO365TokenDeviceCode Example Error: {ex.Message}");
        }
    }
}