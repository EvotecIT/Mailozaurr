namespace Mailozaurr.Application;

/// <summary>
/// Manages profile lifecycle operations for application adapters.
/// </summary>
public interface IMailProfileService {
    /// <summary>Returns all profiles.</summary>
    Task<IReadOnlyList<MailProfile>> GetProfilesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a profile by identifier.</summary>
    Task<MailProfile?> GetProfileAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Validates a profile definition.</summary>
    Task<MailProfileValidationResult> ValidateAsync(MailProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Inspects a saved profile and reports configuration or authentication gaps.</summary>
    Task<MailProfileValidationResult> DiagnoseAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Saves or updates a profile.</summary>
    Task<OperationResult> SaveAsync(MailProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile.</summary>
    Task<OperationResult> DeleteAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Marks a profile as the default profile.</summary>
    Task<OperationResult> SetDefaultAsync(string profileId, CancellationToken cancellationToken = default);

    /// <summary>Returns the effective capabilities for a profile.</summary>
    Task<ProfileCapabilities?> GetCapabilitiesAsync(string profileId, CancellationToken cancellationToken = default);
}