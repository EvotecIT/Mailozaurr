using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves attachments from a Gmail message to disk.
/// </summary>
[Cmdlet(VerbsData.Save, "GmailMessageAttachment")]
public sealed class CmdletSaveGmailMessageAttachment : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(Mandatory = true)]
    public string? Id { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    protected override async Task ProcessRecordAsync() {
        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };
        var client = new GmailApiClient(oauth);
        var list = await client.ListAttachmentsAsync(GmailAccount!, Id!, CancelToken);
        Directory.CreateDirectory(Path!);
        foreach (var att in list) {
            var bytes = await client.DownloadAttachmentAsync(GmailAccount!, Id!, att.Id!, CancelToken);
            var filePath = System.IO.Path.Combine(Path!, att.FileName ?? att.Id);
            File.WriteAllBytes(filePath, bytes);
        }
    }
}
