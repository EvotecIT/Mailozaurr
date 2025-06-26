using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Saves an IMAP message to disk at the specified path.</para>
/// <para type="description">The <c>Save-IMAPMessage</c> cmdlet saves a message from an IMAP mailbox (using a <see cref="ImapConnectionInfo"/> object from <c>Connect-IMAP</c>) to disk at the given path. Provide the unique identifier of the message to export or archive it.</para>
/// <example>
///   <summary>Save an IMAP message to a file</summary>
///   <code>
/// $client = Connect-IMAP ...; Save-IMAPMessage -Client $client -Uid 123 -Path "C:\Mail\message.eml"
/// $client = Connect-IMAP ...; Save-IMAPMessage -Client $client -Uid 123 -Path "C:\Mail\message.msg"
///   </code>
/// </example>
/// <remarks>
/// Use this cmdlet to export or archive messages retrieved from an IMAP server.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.Save, "IMAPMessage")]
public sealed class CmdletSaveIMAPMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the UID of the message to save.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public uint Uid { get; set; }

    /// <summary>
    /// <para type="description">Optional folder name from which to fetch the message. Defaults to Inbox.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public string? Folder { get; set; }

    /// <summary>
    /// <para type="description">Specifies the path where the message will be saved.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 3)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }


    /// <summary>
    /// Saves the specified IMAP message to disk at the given path.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            var uid = new UniqueId(Uid);
            IMailFolder mailFolder = conn.Data.Inbox;
            if (!string.IsNullOrEmpty(Folder)) {
                try {
                    mailFolder = conn.Data.GetFolder(Folder);
                } catch {
                    try {
                        mailFolder = conn.Data.GetFolder(conn.Data.PersonalNamespaces[0]).GetSubfolder(Folder);
                    } catch {
                        WriteWarning($"Save-IMAPMessage - Folder '{Folder}' not found.");
                        return Task.CompletedTask;
                    }
                }
            }
            mailFolder.Open(FolderAccess.ReadOnly);
            var message = mailFolder.GetMessage(uid);
            if (Path != null && Path.EndsWith(".msg", System.StringComparison.OrdinalIgnoreCase)) {
                var tempEml = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid()}.eml");
                message.WriteTo(tempEml);
                EmailMessage.ConvertEmlToMsg(new System.IO.FileInfo(tempEml), new System.IO.FileInfo(Path), true);
                System.IO.File.Delete(tempEml);
            } else {
                message.WriteTo(Path);
            }
        } else {
            WriteWarning("Save-IMAPMessage - Is IMAP connected?");
        }
        return Task.CompletedTask;
    }
}
