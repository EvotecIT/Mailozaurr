namespace Mailozaurr.PowerShell;

/// <summary>Removes a protected secret from a saved mail profile.</summary>
[Cmdlet(VerbsCommon.Remove, "MailProfileSecret", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(OperationResult))]
public sealed class CmdletRemoveMailProfileSecret : MailApplicationCmdletBase {
    /// <summary>Profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Secret name.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string? Name { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        string name = Name!.Trim();
        if (!ShouldProcess($"{profileId}:{name}", "Remove protected mail profile secret")) return;
        OperationResult result = await Application.ProfileSecrets.RemoveSecretAsync(profileId, name, CancelToken)
            .ConfigureAwait(false);
        WriteOperationResult(result, $"{profileId}:{name}");
    }
}
