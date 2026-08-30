namespace Mailozaurr.PowerShell;

/// <summary>Creates a provider profile over the reusable Mailozaurr profile service.</summary>
[Cmdlet(VerbsCommon.New, "MailProfile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(OperationResult))]
public sealed class CmdletNewMailProfile : MailApplicationCmdletBase {
    /// <summary>Stable profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>User-facing profile name.</summary>
    [Parameter(Mandatory = true)]
    public string? DisplayName { get; set; }

    /// <summary>Provider or protocol kind.</summary>
    [Parameter(Mandatory = true)]
    public MailProfileKind Kind { get; set; }

    /// <summary>Optional description.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Default sender address.</summary>
    [Parameter]
    public string? DefaultSender { get; set; }

    /// <summary>Default mailbox or principal.</summary>
    [Parameter]
    public string? DefaultMailbox { get; set; }

    /// <summary>Non-secret provider settings such as server, port, client id, or JMAP session URL.</summary>
    [Parameter]
    [Alias("Setting")]
    public IDictionary? Settings { get; set; }

    /// <summary>Makes this the default profile.</summary>
    [Parameter]
    public SwitchParameter IsDefault { get; set; }

    /// <summary>Allows replacement of an existing profile with the same id.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        string profileId = ProfileId!.Trim();
        MailProfile? existing = await Application.Profiles.GetProfileAsync(profileId, CancelToken).ConfigureAwait(false);
        if (existing != null && !Force) {
            WriteError(new ErrorRecord(
                new InvalidOperationException($"Mail profile '{profileId}' already exists. Use -Force to replace it."),
                "MailProfileAlreadyExists",
                ErrorCategory.ResourceExists,
                profileId));
            return;
        }
        if (!ShouldProcess(profileId, existing == null ? "Create mail profile" : "Replace mail profile")) return;

        var profile = new MailProfile {
            Id = profileId,
            DisplayName = DisplayName!.Trim(),
            Kind = Kind,
            Description = Description,
            DefaultSender = DefaultSender,
            DefaultMailbox = DefaultMailbox,
            IsDefault = IsDefault
        };
        MailProfileCmdletSettings.Merge(profile, Settings);
        OperationResult result;
        if (Force) {
            result = await Application.Profiles.SaveAsync(profile, CancelToken).ConfigureAwait(false);
        } else if (Application.Profiles is IMailProfileCreationService creationService) {
            result = await creationService.CreateAsync(profile, CancelToken).ConfigureAwait(false);
        } else {
            result = OperationResult.Failure(
                "profile_create_not_supported",
                "The configured profile service does not support atomic create-only operations. Use -Force only when replacement is intended.");
        }
        if (result.Succeeded && IsDefault) {
            result = await Application.Profiles.SetDefaultAsync(profileId, CancelToken).ConfigureAwait(false);
        }
        WriteOperationResult(result, profileId);
    }
}
