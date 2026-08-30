namespace Mailozaurr.PowerShell;

/// <summary>Shared application composition for profile-backed PowerShell cmdlets.</summary>
public abstract class MailApplicationCmdletBase : AsyncPSCmdlet {
    private MailApplication? _application;

    /// <summary>Optional directory containing the profile store.</summary>
    [Parameter]
    public string? ProfileDirectory { get; set; }

    /// <summary>Optional directory containing the protected secret store.</summary>
    [Parameter]
    public string? SecretDirectory { get; set; }

    /// <summary>Composed reusable application services for the current cmdlet instance.</summary>
    protected MailApplication Application => _application ??= CreateApplication();

    private MailApplication CreateApplication() {
        var options = new MailApplicationOptions();
        if (!string.IsNullOrWhiteSpace(ProfileDirectory)) {
            options.ProfileStore.DirectoryPath = GetUnresolvedProviderPathFromPSPath(ProfileDirectory!);
        }
        if (!string.IsNullOrWhiteSpace(SecretDirectory)) {
            options.SecretStore.DirectoryPath = GetUnresolvedProviderPathFromPSPath(SecretDirectory!);
        }
        return new MailApplicationBuilder(options).Build();
    }

    /// <summary>Writes a successful operation result or a stable non-terminating PowerShell error.</summary>
    protected void WriteOperationResult(OperationResult result, object target) {
        if (result.Succeeded) {
            WriteObject(result);
            return;
        }

        string message = result.Message ?? "The mail operation failed.";
        WriteError(new ErrorRecord(
            new InvalidOperationException(message),
            string.IsNullOrWhiteSpace(result.Code) ? "MailOperationFailed" : result.Code!,
            ErrorCategory.InvalidOperation,
            target));
    }

    /// <summary>Writes a stable profile-not-found error.</summary>
    protected void WriteProfileNotFound(string profileId) => WriteError(new ErrorRecord(
        new InvalidOperationException($"Mail profile '{profileId}' was not found."),
        "MailProfileNotFound",
        ErrorCategory.ObjectNotFound,
        profileId));
}
