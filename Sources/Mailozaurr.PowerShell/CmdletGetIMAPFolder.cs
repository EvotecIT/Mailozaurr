using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves the IMAP inbox folder and updates message counts for an active IMAP connection.</para>
/// <para type="description">The <c>Get-IMAPFolder</c> cmdlet opens the inbox folder for the provided <see cref="ImapConnectionInfo"/> object (from <c>Connect-IMAP</c>), updates message and recent counts, and returns the updated connection info. Use this to refresh folder state or after connecting to an IMAP server.</para>
/// <example>
///   <summary>Get the inbox folder and message counts</summary>
///   <code>$client = Connect-IMAP ...; Get-IMAPFolder -Client $client</code>
/// </example>
/// <remarks>
/// Use this cmdlet to refresh the folder state after connecting or to update message counts.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "IMAPFolder")]
public sealed class CmdletGetIMAPFolder : AsyncPSCmdlet {
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
    /// Opens the inbox folder, updates message counts, and returns the updated connection info.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var folder = Client.Data.Inbox;
            folder.Open(FolderAccess);
            WriteVerbose($"Get-IMAPFolder - Total messages {folder.Count}, Recent messages {folder.Recent}");
            Client.Messages = folder;
            Client.Count = folder.Count;
            Client.Recent = folder.Recent;
            WriteObject(Client);
        } else {
            WriteVerbose("Get-IMAPFolder - Client not connected?");
        }
        return Task.CompletedTask;
    }
}