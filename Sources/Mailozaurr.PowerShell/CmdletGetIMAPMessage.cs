using System.Collections.Generic;
using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using MailKit.Search;
using MimeKit;
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
    /// <para type="description">Specifies the starting sequence number of messages to retrieve.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public int? SequenceStart { get; set; }

    /// <summary>
    /// <para type="description">Specifies the ending sequence number of messages to retrieve. If not provided, only <c>SequenceStart</c> is fetched.</para>
    /// </summary>
    [Parameter(Position = 3)]
    public int? SequenceEnd { get; set; }

    /// <summary>
    /// <para type="description">Specifies the starting UID of messages to retrieve.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public uint? UidStart { get; set; }

    /// <summary>
    /// <para type="description">Specifies the ending UID of messages to retrieve. If not provided, only <c>UidStart</c> is fetched.</para>
    /// </summary>
    [Parameter(Position = 3)]
    public uint? UidEnd { get; set; }

    /// <summary>
    /// <para type="description">Search query used to match messages to retrieve.</para>
    /// </summary>
    [Parameter]
    public SearchQuery? SearchQuery { get; set; }

    /// <summary>
    /// Opens the inbox folder and retrieves messages if message parameters are specified.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var folder = Client.Folder ?? Client.Data.Inbox;
            folder.Open(FolderAccess);
            WriteVerbose($"Get-IMAPMessage - Total messages {folder.Count}, Recent messages {folder.Recent}");

            List<MimeMessage> messages = new();

            if (SearchQuery != null) {
                var uids = folder.Search(SearchQuery);
                foreach (var uid in uids) {
                    messages.Add(folder.GetMessage(uid));
                }
            } else if (UidStart.HasValue) {
                var uidList = new List<UniqueId>();
                uint endVal = UidEnd ?? UidStart.Value;
                for (uint id = UidStart.Value; id <= endVal; id++)
                    uidList.Add(new UniqueId(id));

                var uids = folder.Search(MailKit.Search.SearchQuery.Uids(uidList));
                foreach (var uid in uids)
                    messages.Add(folder.GetMessage(uid));
            } else if (SequenceStart.HasValue) {
                int start = SequenceStart.Value;
                int end = SequenceEnd ?? SequenceStart.Value;
                for (int i = start; i <= end && i < folder.Count; i++)
                    messages.Add(folder.GetMessage(i));
            }

            if (messages.Count > 0) {
                WriteObject(messages, true);
            } else {
                Client.Folder = folder as MailKit.Net.Imap.ImapFolder;
                WriteObject(Client);
            }
        } else {
            WriteVerbose("Get-IMAPMessage - Client not connected?");
        }
        return Task.CompletedTask;
    }
}