using System.Management.Automation;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using Mailozaurr;

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
    private const string OpenParameterSet = "Open";
    private const string RootParameterSet = "Root";
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object representing the active IMAP connection. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// <para type="description">When specified, lists only the top-level folders instead of opening one.</para>
    /// </summary>
    [Parameter(ParameterSetName = RootParameterSet)]
    public SwitchParameter Root { get; set; }

    /// <summary>
    /// <para type="description">Folder path to open. Defaults to the inbox when not provided.</para>
    /// </summary>
    [Parameter(ParameterSetName = OpenParameterSet)]
    public string? Path { get; set; }

    /// <summary>
    /// <para type="description">Specifies the folder access mode (ReadOnly or ReadWrite). Default is ReadOnly.</para>
    /// </summary>
    [Parameter(Position = 1, ParameterSetName = OpenParameterSet)]
    public FolderAccess FolderAccess { get; set; } = FolderAccess.ReadOnly;

    /// <summary>
    /// Opens the inbox folder, updates message counts, and returns the updated connection info.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var conn = Client ?? DefaultSessions.ImapSession;
        if (conn != null && conn.Data != null) {
            if (Root) {
                await foreach (var folder in ImapRootFolderEnumerator.EnumerateAsync(conn.Data, CancelToken)) {
                    WriteObject(folder);
                }
            } else {
                var name = string.IsNullOrWhiteSpace(Path) ? conn.Folder?.FullName : Path;
                var folder = (ImapFolder)conn.Data.GetCachedFolder(name, FolderAccess);
                WriteVerbose($"Get-IMAPFolder - Total messages {folder.Count}, Recent messages {folder.Recent}");
                conn.Messages = folder;
                conn.Count = folder.Count;
                conn.Recent = folder.Recent;
                conn.Folder = folder;
                conn.Folders[folder.FullName] = folder;
                DefaultSessions.ImapSession = conn;
                WriteObject(conn);
            }
        } else {
            ThrowTerminatingError(new ErrorRecord(
                new InvalidOperationException("Get-IMAPFolder - IMAP client not provided or not connected."),
                "ClientNotConnected",
                ErrorCategory.InvalidOperation,
                null));
        }
    }}