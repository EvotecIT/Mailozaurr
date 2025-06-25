using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Set, "GraphMessage")]
[Alias("Set-MailMessage")]
public class CmdletSetGraphMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    [Parameter]
    public SwitchParameter Read { get; set; }

    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }


    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    protected override Task ProcessRecordAsync() {
        switch (ParameterSetName) {
            case "Graph":
                var conn = Connection ?? DefaultSessions.GraphSession;
                if (conn == null) {
                    WriteWarning("Set-GraphMessage - Connection not provided and no default session available.");
                    return Task.CompletedTask;
                }
                return ProcessGraphAsync(conn.Credential);
            case "MgGraphRequest":
                ProcessMgGraph();
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        await MicrosoftGraphUtils.SetMailMessageAsync(cred, UserPrincipalName!, MessageId!, Read.IsPresent);
    }

    private void ProcessMgGraph() {
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{MessageId}";
        var body = JsonSerializer.Serialize(new { isRead = Read.IsPresent });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "PATCH")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
    }
}
