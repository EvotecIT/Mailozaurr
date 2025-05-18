using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr.PowerShell;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves messages from a POP3 mailbox using an active POP3 connection.</para>
/// <para type="description">The <c>Get-POPMessage</c> cmdlet retrieves one or more messages from a POP3 mailbox using the provided <see cref="PopConnectionInfo"/> object (from <c>Connect-POP</c>). You can specify the message index, count, or use <c>-All</c> to retrieve all messages. Returns message objects for further automation or archiving.</para>
/// <example>
///   <summary>Get the first message from a POP3 mailbox</summary>
///   <code>$client = Connect-POP ...; Get-POPMessage -Client $client -Index 0</code>
/// </example>
/// <example>
///   <summary>Get all messages from a POP3 mailbox</summary>
///   <code>$client = Connect-POP ...; Get-POPMessage -Client $client -All</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate or download messages from a POP3 mailbox for backup, migration, or processing.
/// </remarks>
/// <seealso cref="CmdletConnectPOP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "POPMessage")]
[Alias("Get-POP3Message")]
public sealed class CmdletGetPOPMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="PopConnectionInfo"/> object representing the active POP3 connection. This is the object returned by <c>Connect-POP</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PopConnectionInfo Client { get; set; }

    /// <summary>
    /// <para type="description">Specifies the index of the first message to retrieve. Default is 0 (the first message).</para>
    /// </summary>
    [Parameter(Position = 1)]
    public int Index { get; set; }

    /// <summary>
    /// <para type="description">Specifies the number of messages to retrieve starting from <c>Index</c>. Default is 1.</para>
    /// </summary>
    [Parameter(Position = 2)]
    public int Count { get; set; } = 1;

    /// <summary>
    /// <para type="description">If set, retrieves all messages from the POP3 mailbox.</para>
    /// </summary>
    [Parameter()]
    public SwitchParameter All { get; set; }

    /// <summary>
    /// Retrieves one or more messages from the POP3 mailbox.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null && Client.Data != null) {
            if (All.IsPresent) {
                var messages = Client.Data.GetMessages(Index, Count);
                WriteObject(messages, true);
            } else {
                if (Index < Client.Data.Count) {
                    var messages = Client.Data.GetMessages(Index, Count);
                    WriteObject(messages, true);
                } else {
                    WriteWarning($"Get-POP3Message - Index is out of range. Use index less than {Client.Data.Count}.");
                }
            }
        } else {
            WriteWarning("Get-POP3Message - Is POP3 connected?");
        }
        return Task.CompletedTask;
    }
}