using Mailozaurr;
using System;
using System.Threading.Tasks;

public static class FetchGraphFolders
{
    public static async Task RunAsync()
    {
        string clientId = "your-client-id";
        string tenantId = "your-tenant-id";
        string clientSecret = "your-client-secret";
        string user = "user@example.com";

        var cred = new GraphCredential { ClientId = clientId, DirectoryId = tenantId, ClientSecret = clientSecret };
        var folders = await MicrosoftGraphUtils.GetMailFolderInfosAsync(cred, user);
        foreach (var f in folders)
        {
            Console.WriteLine($"{f.FullPath} - Total {f.TotalItemCount} Unread {f.UnreadItemCount}");
        }
    }
}
