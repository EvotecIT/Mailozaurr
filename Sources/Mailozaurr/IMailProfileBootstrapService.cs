namespace Mailozaurr;

/// <summary>
/// Creates or updates reusable provider profiles using higher-level bootstrap requests.
/// </summary>
public interface IMailProfileBootstrapService {
    /// <summary>Creates or updates a Microsoft Graph profile and any associated secrets.</summary>
    Task<OperationResult> SaveGraphProfileAsync(GraphProfileBootstrapRequest request, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a Gmail profile and any associated secrets.</summary>
    Task<OperationResult> SaveGmailProfileAsync(GmailProfileBootstrapRequest request, CancellationToken cancellationToken = default);
}