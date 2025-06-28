namespace Mailozaurr;

/// <summary>
/// Represents actions that can be performed on a Microsoft Graph message.
/// </summary>
public enum GraphMessageAction {
    /// <summary>Move a message to another folder.</summary>
    Move,
    /// <summary>Copy a message to another folder.</summary>
    Copy,
    /// <summary>Delete a message.</summary>
    Delete
}
