namespace Mailozaurr;

/// <summary>
/// Represents actions that can be performed on a Microsoft Graph message.
/// </summary>
/// <remarks>
/// These are simplified descriptions of common message operations
/// available in the Graph API.
/// </remarks>
public enum GraphMessageAction {
    /// <summary>Move a message to another folder.</summary>
    Move,
    /// <summary>Copy a message to another folder.</summary>
    Copy,
    /// <summary>Delete a message.</summary>
    Delete
}
