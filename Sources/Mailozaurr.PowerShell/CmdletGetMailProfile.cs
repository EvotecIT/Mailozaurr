namespace Mailozaurr.PowerShell;

/// <summary>Gets one or more saved Mailozaurr profiles without exposing secret values.</summary>
[Cmdlet(VerbsCommon.Get, "MailProfile")]
[OutputType(typeof(MailProfile))]
public sealed class CmdletGetMailProfile : MailApplicationCmdletBase {
    /// <summary>Optional profile identifier. All profiles are returned when omitted.</summary>
    [Parameter(Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Optional provider kind filter when listing profiles.</summary>
    [Parameter]
    public MailProfileKind? Kind { get; set; }

    /// <summary>Returns only the default profile.</summary>
    [Parameter]
    public SwitchParameter DefaultOnly { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        if (!string.IsNullOrWhiteSpace(ProfileId)) {
            MailProfile? profile = await Application.Profiles.GetProfileAsync(ProfileId!.Trim(), CancelToken)
                .ConfigureAwait(false);
            if (profile == null) {
                WriteProfileNotFound(ProfileId!);
                return;
            }
            WriteObject(profile);
            return;
        }

        IReadOnlyList<MailProfile> profiles = await Application.Profiles.GetProfilesAsync(CancelToken)
            .ConfigureAwait(false);
        foreach (MailProfile profile in profiles) {
            if (Kind.HasValue && profile.Kind != Kind.Value) continue;
            if (DefaultOnly && !profile.IsDefault) continue;
            WriteObject(profile);
        }
    }
}
