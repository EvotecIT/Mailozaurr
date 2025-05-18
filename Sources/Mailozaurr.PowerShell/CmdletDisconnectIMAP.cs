namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Disconnects an active IMAP connection.</para>
/// <para type="description">This cmdlet disconnects an active MailKit IMAP client session. Pass the ImapConnectionInfo object returned by Connect-IMAP.</para>
/// <example>
/// <para>Disconnect an IMAP client</para>
/// <code>$client = Connect-IMAP ...; Disconnect-IMAP -Client $client</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "IMAP")]
public sealed class CmdletDisconnectIMAP : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The ImapConnectionInfo object containing the MailKit IMAP client instance to disconnect. This is the object returned by Connect-IMAP.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    public ImapConnectionInfo Client { get; set; }

    /// <summary>
    /// <para type="description">Performs the disconnect operation on the provided IMAP client. Writes a warning if disconnection fails.</para>
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