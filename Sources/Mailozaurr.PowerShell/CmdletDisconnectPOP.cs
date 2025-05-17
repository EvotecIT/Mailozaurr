using System.Management.Automation;
using System.Threading.Tasks;
using MailKit.Net.Pop3;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Disconnects an active POP3 connection.</para>
/// <para type="description">This cmdlet disconnects an active MailKit POP3 client session. Pass the PopConnectionInfo object returned by Connect-POP.</para>
/// <example>
/// <para>Disconnect a POP3 client</para>
/// <code>$client = Connect-POP ...; Disconnect-POP -Client $client</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "POP")]
[Alias("Disconnect-POP3")]
public sealed class CmdletDisconnectPOP : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The PopConnectionInfo object containing the MailKit POP3 client instance to disconnect. This is the object returned by Connect-POP.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public PopConnectionInfo Client { get; set; }

    /// <summary>
    /// <para type="description">Performs the disconnect operation on the provided POP3 client. Writes a warning if disconnection fails.</para>
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var data = Client.Data;
            if (data != null) {
                try {
                    data.Disconnect(true);
                } catch (System.Exception ex) {
                    WriteWarning($"Disconnect-POP - Unable to disconnect: {ex.Message}");
                }
            } else {
                WriteWarning("Disconnect-POP - The provided object does not contain a valid Data property of type Pop3Client.");
            }
        }
        return Task.CompletedTask;
    }
}