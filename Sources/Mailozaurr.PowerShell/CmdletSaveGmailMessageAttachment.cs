using System.IO;
using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves attachments from a Gmail message to disk.
/// </summary>
[Cmdlet(VerbsData.Save, "GmailMessageAttachment")]
public sealed class CmdletSaveGmailMessageAttachment : AsyncPSCmdlet {
    /// <summary>
    /// Gmail account containing the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// OAuth credential used for authentication.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Identifier of the Gmail message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? Id { get; set; }

    /// <summary>
    /// Destination path for saving attachments.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    /// <summary>
    /// Downloads attachments from the specified Gmail message.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
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
            var filePath = System.IO.Path.Combine(Path!, att.FileName ?? att.Id!);
            File.WriteAllBytes(filePath, bytes);
        }
    }
}