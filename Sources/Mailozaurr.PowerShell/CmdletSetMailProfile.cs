namespace Mailozaurr.PowerShell;

/// <summary>Updates non-secret fields of an existing Mailozaurr profile.</summary>
[Cmdlet(VerbsCommon.Set, "MailProfile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(OperationResult))]
public sealed class CmdletSetMailProfile : MailApplicationCmdletBase {
    /// <summary>Profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Updated user-facing name.</summary>
    [Parameter]
    public string? DisplayName { get; set; }

    /// <summary>Updated provider kind.</summary>
    [Parameter]
    public MailProfileKind Kind { get; set; }

    /// <summary>Updated description. Pass an empty string to clear it.</summary>
    [Parameter]
    [AllowEmptyString]
    public string? Description { get; set; }

    /// <summary>Updated default sender. Pass an empty string to clear it.</summary>
    [Parameter]
    [AllowEmptyString]
    public string? DefaultSender { get; set; }

    /// <summary>Updated default mailbox. Pass an empty string to clear it.</summary>
    [Parameter]
    [AllowEmptyString]
    public string? DefaultMailbox { get; set; }

    /// <summary>Non-secret settings to merge into the profile.</summary>
    [Parameter]
    [Alias("Setting")]
    public IDictionary? Settings { get; set; }

    /// <summary>Setting names to remove.</summary>
    [Parameter]
    public string[]? RemoveSetting { get; set; }

    /// <summary>Clears all non-secret settings before applying <see cref="Settings"/>.</summary>
    [Parameter]
    public SwitchParameter ClearSettings { get; set; }

    /// <summary>Makes this the default profile.</summary>
    [Parameter]
    public SwitchParameter IsDefault { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        MailProfile? profile = await Application.Profiles.GetProfileAsync(profileId, CancelToken).ConfigureAwait(false);
        if (profile == null) {
            WriteProfileNotFound(profileId);
            return;
        }
        if (!ShouldProcess(profileId, "Update mail profile")) return;

        if (MyInvocation.BoundParameters.ContainsKey(nameof(DisplayName))) profile.DisplayName = DisplayName?.Trim() ?? string.Empty;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Kind))) profile.Kind = Kind;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Description))) profile.Description = EmptyToNull(Description);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultSender))) profile.DefaultSender = EmptyToNull(DefaultSender);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DefaultMailbox))) profile.DefaultMailbox = EmptyToNull(DefaultMailbox);
        if (ClearSettings) profile.Settings.Clear();
        foreach (string name in RemoveSetting ?? Array.Empty<string>()) {
            if (!string.IsNullOrWhiteSpace(name)) profile.Settings.Remove(name.Trim());
        }
        MailProfileCmdletSettings.Merge(profile, Settings);

        OperationResult result = await Application.Profiles.SaveAsync(profile, CancelToken).ConfigureAwait(false);
        if (result.Succeeded && IsDefault) {
            result = await Application.Profiles.SetDefaultAsync(profileId, CancelToken).ConfigureAwait(false);
        }
        WriteOperationResult(result, profileId);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
}
