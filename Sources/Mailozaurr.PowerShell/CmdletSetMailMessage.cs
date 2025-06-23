using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommon.Set, "MailMessage")]
public class CmdletSetMailMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }

    [Parameter]
    public SwitchParameter Read { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphCredential")]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphConnection")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "GraphCredential")]
    [Parameter(Mandatory = true, ParameterSetName = "GraphConnection")]
    public SwitchParameter Graph { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    protected override Task ProcessRecordAsync() {
        return ParameterSetName switch {
            "GraphCredential" => ProcessGraphAsync(GetGraphCredential()),
            "GraphConnection" => ProcessGraphAsync(Connection!.Credential),
            "MgGraphRequest" => ProcessMgGraphAsync(),
            _ => Task.CompletedTask
        };
    }

    private GraphCredential GetGraphCredential() => MicrosoftGraphUtils.ConvertFromGraphCredential(
        Credential!.UserName,
        Credential.GetNetworkCredential().Password);

    private async Task ProcessGraphAsync(GraphCredential cred) {
        await MicrosoftGraphUtils.SetMailMessageAsync(cred, UserPrincipalName!, MessageId!, Read.IsPresent);
    }

    private Task ProcessMgGraphAsync() {
        var uri = $"https://graph.microsoft.com/v1.0/users/{UserPrincipalName}/messages/{MessageId}";
        var body = JsonSerializer.Serialize(new { isRead = Read.IsPresent });
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "PATCH")
            .AddParameter("Uri", uri)
            .AddParameter("Body", body)
            .AddParameter("ContentType", "application/json");
        ps.Invoke();
        return Task.CompletedTask;
    }
}
