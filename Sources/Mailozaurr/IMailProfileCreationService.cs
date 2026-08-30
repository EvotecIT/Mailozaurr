namespace Mailozaurr;

/// <summary>
/// Creates profiles without replacing a profile that appeared concurrently.
/// </summary>
public interface IMailProfileCreationService {
    /// <summary>
    /// Creates a profile only when its identifier is still unused.
    /// </summary>
    Task<OperationResult> CreateAsync(MailProfile profile, CancellationToken cancellationToken = default);
}
