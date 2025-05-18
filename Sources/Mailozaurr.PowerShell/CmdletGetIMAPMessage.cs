using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves the IMAP inbox folder and prepares the client for message retrieval.</para>
/// <para type="description">The <c>Get-IMAPMessage</c> cmdlet opens the inbox folder for the provided <see cref="ImapConnectionInfo"/> object (from <c>Connect-IMAP</c>), sets up the folder for message retrieval, and returns the updated connection info. Use this before fetching messages from the IMAP server.</para>
/// <example>
///   <summary>Prepare the IMAP client for message retrieval</summary>
///   <code>$client = Connect-IMAP ...; Get-IMAPMessage -Client $client</code>
/// </example>
/// <remarks>
/// Use this cmdlet to open the inbox and prepare for message enumeration or retrieval.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "IMAPMessage")]
public sealed class CmdletGetIMAPMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the folder access mode (ReadOnly or ReadWrite). Default is ReadOnly.</para>
    /// </summary>
    [Parameter(Position = 1)]
    public FolderAccess FolderAccess { get; set; } = FolderAccess.ReadOnly;

    /// <summary>
    /// Opens the inbox folder and prepares the client for message retrieval.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var folder = Client.Data.Inbox;
            folder.Open(FolderAccess);
            WriteVerbose($"Get-IMAPMessage - Total messages {folder.Count}, Recent messages {folder.Recent}");
            Client.Folder = folder as MailKit.Net.Imap.ImapFolder;
            WriteObject(Client);
        } else {
            WriteVerbose("Get-IMAPMessage - Client not connected?");
        }
        return Task.CompletedTask;
    }
}