using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Moves an IMAP message to another folder.</para>
/// <para type="description">The <c>Move-IMAPMessage</c> cmdlet moves a message identified by its UID from the current folder to the specified destination folder.</para>
/// <example>
///   <summary>Move a message to Archive</summary>
///   <code>$client = Connect-IMAP ...; Move-IMAPMessage -Client $client -Uid 10 -DestinationFolder "Archive"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to organize messages on an IMAP server.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Move, "IMAPMessage", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public sealed class CmdletMoveIMAPMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">UID of the message to move.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 1)]
    public uint Uid { get; set; }

    /// <summary>
    /// <para type="description">Optional source folder of the message. Defaults to Inbox.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public string? SourceFolder { get; set; }

    /// <summary>
    /// <para type="description">Destination folder where the message should be moved.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 3)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolder { get; set; }

    /// <summary>
    /// Moves the specified message to the destination folder.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (!ShouldProcess(Uid.ToString(), $"Moving IMAP message to {DestinationFolder}")) {
                return Task.CompletedTask;
            }
            var uid = new UniqueId(Uid);
            var source = conn.Data.GetCachedFolder(SourceFolder ?? conn.Folder?.FullName, FolderAccess.ReadWrite);
            var dest = conn.Data.GetCachedFolder(DestinationFolder!, FolderAccess.ReadWrite);
            conn.Folders[source.FullName] = (ImapFolder)source;
            conn.Folders[dest.FullName] = (ImapFolder)dest;
            source.MoveTo(uid, dest);
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Move-IMAPMessage - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
        return Task.CompletedTask;
    }
}
