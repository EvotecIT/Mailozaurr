namespace Mailozaurr;

/// <summary>
/// Indicates the priority of an email message.
/// </summary>
/// <remarks>
/// Many mail clients surface priority as "importance" when
/// displaying messages to end users.
/// </remarks>
public enum MessagePriority {
    /// <summary>
    /// High priority message.
    /// </summary>
    High,

    /// <summary>
    /// Low priority message.
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority message.
    /// </summary>
    Normal
}
