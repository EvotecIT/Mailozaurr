namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Disconnects an active IMAP connection previously established with Connect-IMAP.</para>
/// <para type="description">The <c>Disconnect-IMAP</c> cmdlet disconnects an active MailKit IMAP client session. Pass the <see cref="ImapConnectionInfo"/> object returned by <c>Connect-IMAP</c> to this cmdlet to safely close the connection and release resources.</para>
/// <example>
///   <summary>Disconnect an IMAP client</summary>
///   <code>$client = Connect-IMAP ...; Disconnect-IMAP -Client $client</code>
/// </example>
/// <remarks>
/// Always disconnect IMAP sessions to avoid resource leaks and server-side session limits.
/// </remarks>
/// <seealso cref="CmdletConnectIMAP"/>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "IMAP")]
public sealed class CmdletDisconnectIMAP : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="ImapConnectionInfo"/> object containing the MailKit IMAP client instance to disconnect. This is the object returned by <c>Connect-IMAP</c>.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public ImapConnectionInfo? Client { get; set; }

    /// <summary>
    /// Disconnects the provided IMAP client. Writes a warning if disconnection fails.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (Client != null) {
            var data = Client.Data;
            if (data != null) {
                try {
                    data.Disconnect(true);
                } catch (System.Exception ex) {
                    WriteWarning($"Disconnect-IMAP - Unable to disconnect: {ex.Message}");
                }
            } else {
                WriteWarning("Disconnect-IMAP - The provided object does not contain a valid Data property of type ImapClient.");
            }
        }
        return Task.CompletedTask;
    }
}