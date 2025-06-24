using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Move, "MailMessage")]
public class CmdletMoveMailMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? DestinationFolderId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    public SwitchParameter Graph { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    protected override Task ProcessRecordAsync() {
        return ParameterSetName switch {
            "Graph" => ProcessGraphAsync(Connection!.Credential),
            "MgGraphRequest" => ProcessMgGraphAsync(),
            _ => Task.CompletedTask
        };
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        await MicrosoftGraphUtils.MoveMailMessageAsync(cred, UserPrincipalName!, MessageId!, DestinationFolderId!);
    }

    private Task ProcessMgGraphAsync() {
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{MessageId}/move";
        var body = System.Text.Json.JsonSerializer.Serialize(new { destinationId = DestinationFolderId });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "POST")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
        return Task.CompletedTask;
    }
}
