namespace Mailozaurr.PowerShell;

/// <summary>Diagnoses stored profile readiness or performs a live connection probe.</summary>
[Cmdlet(VerbsDiagnostic.Test, "MailProfile")]
[OutputType(typeof(MailProfileValidationResult), typeof(MailProfileConnectionTestResult))]
public sealed class CmdletTestMailProfile : MailApplicationCmdletBase {
    /// <summary>Profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Runs a live provider connection probe instead of stored readiness diagnosis.</summary>
    [Parameter]
    public SwitchParameter Connection { get; set; }

    /// <summary>Depth of the live connection probe.</summary>
    [Parameter]
    public MailProfileConnectionTestScope Scope { get; set; } = MailProfileConnectionTestScope.Auto;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        object result = Connection
            ? await Application.ProfileConnections.TestAsync(profileId, Scope, CancelToken).ConfigureAwait(false)
            : await Application.Profiles.DiagnoseAsync(profileId, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}
