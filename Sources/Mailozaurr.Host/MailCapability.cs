namespace Mailozaurr.Hosting;

/// <summary>
/// Describes normalized capabilities that a profile can expose across adapters.
/// </summary>
[Flags]
public enum MailCapability {
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>List folders or mailbox containers.</summary>
    ListFolders = 1 << 0,

    /// <summary>Search messages.</summary>
    SearchMessages = 1 << 1,

    /// <summary>Read message details.</summary>
    ReadMessages = 1 << 2,

    /// <summary>Save attachments to disk or other storage.</summary>
    SaveAttachments = 1 << 3,

    /// <summary>Mark messages read, unread, flagged, or equivalent.</summary>
    MarkMessages = 1 << 4,

    /// <summary>Move messages between folders or labels.</summary>
    MoveMessages = 1 << 5,

    /// <summary>Delete messages.</summary>
    DeleteMessages = 1 << 6,

    /// <summary>Send messages.</summary>
    SendMessages = 1 << 7,

    /// <summary>Wait for or subscribe to new messages.</summary>
    WaitForMessages = 1 << 8,

    /// <summary>Manage inbox rules or equivalent server-side rules.</summary>
    ManageRules = 1 << 9,

    /// <summary>Manage calendar events exposed through the same provider.</summary>
    ManageEvents = 1 << 10,

    /// <summary>Manage mailbox permissions.</summary>
    ManagePermissions = 1 << 11,

    /// <summary>Work with threaded conversations.</summary>
    UseThreads = 1 << 12,

    /// <summary>Work with labels or label-like grouping.</summary>
    UseLabels = 1 << 13,
}