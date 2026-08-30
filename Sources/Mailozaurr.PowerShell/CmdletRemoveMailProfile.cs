namespace Mailozaurr.PowerShell;

/// <summary>Removes a profile and its owned protected secrets.</summary>
[Cmdlet(VerbsCommon.Remove, "MailProfile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(OperationResult))]
public sealed class CmdletRemoveMailProfile : MailApplicationCmdletBase {
    /// <summary>Profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        if (!ShouldProcess(profileId, "Remove mail profile and owned secrets")) return;
        OperationResult result = await Application.Profiles.DeleteAsync(profileId, CancelToken).ConfigureAwait(false);
        WriteOperationResult(result, profileId);
    }
}
