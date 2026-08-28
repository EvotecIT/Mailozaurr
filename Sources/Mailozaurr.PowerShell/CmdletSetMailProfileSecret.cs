using System.Security;

namespace Mailozaurr.PowerShell;

/// <summary>Stores or copies a protected secret for a saved mail profile.</summary>
[Cmdlet(VerbsCommon.Set, "MailProfileSecret", DefaultParameterSetName = ValueParameterSet,
    SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(OperationResult))]
public sealed class CmdletSetMailProfileSecret : MailApplicationCmdletBase {
    private const string ValueParameterSet = "Value";
    private const string ReferenceParameterSet = "Reference";

    /// <summary>Profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Well-known or provider-specific secret name.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string? Name { get; set; }

    /// <summary>Secret value supplied through PowerShell's protected string type.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ValueParameterSet)]
    public SecureString? Value { get; set; }

    /// <summary>Stored secret reference in <c>profile-id:secret-name</c> form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ReferenceParameterSet)]
    public string? Reference { get; set; }

    /// <summary>Explicitly permits a reference owned by another profile.</summary>
    [Parameter(ParameterSetName = ReferenceParameterSet)]
    public SwitchParameter AllowCrossProfileReference { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        string name = Name!.Trim();
        if (!ShouldProcess($"{profileId}:{name}", "Store protected mail profile secret")) return;

        string? clearValue = ParameterSetName == ValueParameterSet
            ? CredentialHelpers.ToPlainText(Value)
            : null;
        try {
            OperationResult result = await Application.ProfileSecrets.SetSecretAsync(
                profileId,
                name,
                clearValue,
                Reference,
                AllowCrossProfileReference,
                CancelToken).ConfigureAwait(false);
            WriteOperationResult(result, $"{profileId}:{name}");
        } finally {
            clearValue = null;
        }
    }
}
