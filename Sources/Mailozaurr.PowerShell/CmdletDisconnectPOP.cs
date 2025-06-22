using System.Management.Automation;
using System.Threading.Tasks;
using MailKit.Net.Pop3;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Disconnects an active POP3 connection previously established with Connect-POP.</para>
/// <para type="description">The <c>Disconnect-POP</c> cmdlet disconnects an active MailKit POP3 client session. Pass the <see cref="PopConnectionInfo"/> object returned by <c>Connect-POP</c> to this cmdlet to safely close the connection and release resources.</para>
/// <example>
///   <summary>Disconnect a POP3 client</summary>
///   <code>$client = Connect-POP ...; Disconnect-POP -Client $client</code>
/// </example>
/// <remarks>
/// Always disconnect POP3 sessions to avoid resource leaks and server-side session limits.
/// </remarks>
/// <seealso cref="CmdletConnectPOP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "POP")]
[Alias("Disconnect-POP3")]
public sealed class CmdletDisconnectPOP : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="PopConnectionInfo"/> object containing the MailKit POP3 client instance to disconnect. This is the object returned by <c>Connect-POP</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public PopConnectionInfo? Client { get; set; }

    /// <summary>
    /// Disconnects the provided POP3 client. Writes a warning if disconnection fails.
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