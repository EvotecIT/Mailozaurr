using System.Security.Cryptography;
using System.Text;

namespace Mailozaurr;

/// <summary>
/// Generates stable confirmation tokens that tie previews to destructive mailbox actions.
/// </summary>
public static class MessageActionConfirmationTokens {
    /// <summary>Creates a confirmation token for a move-like action.</summary>
    public static string CreateMoveToken(
        string profileId,
        string? mailboxId,
        string? folderId,
        IEnumerable<string> messageIds,
        string destinationFolderId) =>
        CreateToken("move", profileId, mailboxId, folderId, messageIds, destinationFolderId);

    /// <summary>Creates a confirmation token for a delete action.</summary>
    public static string CreateDeleteToken(
        string profileId,
        string? mailboxId,
        string? folderId,
        IEnumerable<string> messageIds) =>
        CreateToken("delete", profileId, mailboxId, folderId, messageIds, destinationFolderId: null);

    /// <summary>Creates a confirmation token for a read/unread state change.</summary>
    public static string CreateReadStateToken(
        string profileId,
        string? mailboxId,
        string? folderId,
        IEnumerable<string> messageIds,
        bool isRead) =>
        CreateToken(isRead ? "read-state-read" : "read-state-unread", profileId, mailboxId, folderId, messageIds, destinationFolderId: null);

    /// <summary>Creates a confirmation token for a flagged/unflagged state change.</summary>
    public static string CreateFlaggedStateToken(
        string profileId,
        string? mailboxId,
        string? folderId,
        IEnumerable<string> messageIds,
        bool isFlagged) =>
        CreateToken(isFlagged ? "flagged-state-flagged" : "flagged-state-unflagged", profileId, mailboxId, folderId, messageIds, destinationFolderId: null);

    private static string CreateToken(
        string action,
        string profileId,
        string? mailboxId,
        string? folderId,
        IEnumerable<string> messageIds,
        string? destinationFolderId) {
        if (string.IsNullOrWhiteSpace(action)) {
            throw new ArgumentException("Action is required.", nameof(action));
        }
        if (string.IsNullOrWhiteSpace(profileId)) {
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        }
        if (messageIds == null) {
            throw new ArgumentNullException(nameof(messageIds));
        }

        var normalizedMessageIds = messageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var payload = string.Join("|", new[] {
            "mail-action-confirmation-v1",
            action.Trim().ToLowerInvariant(),
            profileId.Trim(),
            mailboxId?.Trim() ?? string.Empty,
            folderId?.Trim() ?? string.Empty,
            destinationFolderId?.Trim() ?? string.Empty,
            string.Join(",", normalizedMessageIds)
        });

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"mact_v1_{token}";
    }
}