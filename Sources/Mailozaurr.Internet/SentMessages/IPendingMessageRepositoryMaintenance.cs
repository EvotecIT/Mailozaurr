namespace Mailozaurr;

/// <summary>
/// Exposes maintenance that must finish before a short-lived queue processor exits.
/// </summary>
public interface IPendingMessageRepositoryMaintenance {
    /// <summary>
    /// Waits for maintenance already scheduled by committed queue mutations.
    /// </summary>
    Task WaitForPendingMaintenanceAsync();
}
