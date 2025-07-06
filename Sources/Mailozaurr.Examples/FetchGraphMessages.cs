using Mailozaurr;
using System;
using System.Threading.Tasks;

public static class FetchGraphMessages {
    public static async Task RunAsync() {
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string clientSecret = "your-client-secret";
        string user = "user@example.com";

        var cred = new GraphCredential { ClientId = clientId, DirectoryId = tenantId, ClientSecret = clientSecret };
        var messages = await MicrosoftGraphUtils.GetMailMessageInfosAsync(cred, user, limit: 5);
        foreach (var m in messages) {
            Console.WriteLine($"{m.Subject} - {m.ReceivedDate:G} Attachments: {m.HasAttachments}");
        }
    }
}

