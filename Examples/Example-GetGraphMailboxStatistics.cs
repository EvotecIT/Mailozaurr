using System.Management.Automation;
using System.Threading.Tasks;
using Mailozaurr;

/// <summary>
/// Example cmdlet demonstrating how to expose mailbox statistics from
/// <see cref="MicrosoftGraphUtils"/> in PowerShell.
/// </summary>
[Cmdlet(VerbsCommon.Get, "ExampleGraphMailboxStatistics")]
[OutputType(typeof(GraphMailboxStatistics))]
public sealed class GetExampleGraphMailboxStatisticsCommand : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public GraphCredential? Credential { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    protected override async Task ProcessRecordAsync() {
        var stats = await MicrosoftGraphUtils.GetMailboxStatisticsAsync(
            Credential!,
            UserPrincipalName!);
        WriteObject(stats);
    }
}
