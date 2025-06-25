using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Clears Microsoft Graph credentials created by Connect-EmailGraph.</para>
/// <para type="description">The <c>Disconnect-EmailGraph</c> cmdlet removes sensitive
/// information from a <see cref="GraphConnectionInfo"/> object returned by
/// <c>Connect-EmailGraph</c>. Use it when you no longer need the connection to
/// ensure credentials are disposed and not kept in memory.</para>
/// <example>
///   <summary>Disconnect from Microsoft Graph</summary>
///   <code>$graph = Connect-EmailGraph ...; Disconnect-EmailGraph -Connection $graph</code>
/// </example>
/// <remarks>
/// This cmdlet does not close any network connections but clears the stored
/// client secret and marks the connection as disconnected.
/// </remarks>
/// <seealso cref="CmdletConnectEmailGraph"/>
/// </summary>
[Cmdlet(VerbsCommunications.Disconnect, "EmailGraph")]
public sealed class CmdletDisconnectEmailGraph : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">The <see cref="GraphConnectionInfo"/> object to clear.</para>
    /// </summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    /// <summary>Clears the credential stored in the connection object.</summary>
    protected override Task ProcessRecordAsync() {
        if (Connection != null) {
            var cred = Connection.Credential;
            if (cred != null) {
                cred.ClientSecret = string.Empty;
            }
            Connection.IsConnected = false;
        }
        return Task.CompletedTask;
    }
}
