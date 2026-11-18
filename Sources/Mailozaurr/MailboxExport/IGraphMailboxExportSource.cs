using System.Text.Json;

namespace Mailozaurr.MailboxExport;

/// <summary>Mailbox content source read by <see cref="GraphMailboxPstExporter"/>.</summary>
/// <remarks>
/// The Microsoft Graph implementation is <see cref="GraphMailboxExportSource"/>. Other implementations
/// can supply the same folder, message, contact, calendar and task pages from a different service.
/// </remarks>
public interface IGraphMailboxExportSource {
    /// <summary>Returns every mail folder in the mailbox, parents before children.</summary>
    Task<IReadOnlyList<GraphExportFolder>> GetMailFoldersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns one page of messages from a mail folder, ordered by received time ascending.
    /// </summary>
    /// <param name="folderId">Source folder identifier.</param>
    /// <param name="receivedFrom">Inclusive lower bound on received time, or null.</param>
    /// <param name="receivedBefore">Exclusive upper bound on received time, or null.</param>
    /// <param name="pageLink">Continuation link from a previous page, or null for the first page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GraphExportPage<GraphExportMessageInfo>> GetMessagesAsync(
        string folderId,
        DateTimeOffset? receivedFrom,
        DateTimeOffset? receivedBefore,
        string? pageLink,
        CancellationToken cancellationToken);

    /// <summary>Downloads the RFC 822 MIME content of one message.</summary>
    Task<byte[]> GetMessageMimeAsync(string messageId, long maxBytes, CancellationToken cancellationToken);

    /// <summary>Returns the default contacts folder and its subfolders, parents before children.</summary>
    Task<IReadOnlyList<GraphExportFolder>> GetContactFoldersAsync(CancellationToken cancellationToken);

    /// <summary>Returns one page of Graph contact resources from a contact folder.</summary>
    Task<GraphExportPage<JsonElement>> GetContactsAsync(string folderId, string? pageLink, CancellationToken cancellationToken);

    /// <summary>Returns the mailbox calendars. The default calendar has the Calendar role.</summary>
    Task<IReadOnlyList<GraphExportFolder>> GetCalendarsAsync(CancellationToken cancellationToken);

    /// <summary>Returns one page of Graph event resources (single instances and series masters) from a calendar.</summary>
    Task<GraphExportPage<JsonElement>> GetEventsAsync(string calendarId, string? pageLink, CancellationToken cancellationToken);

    /// <summary>Returns the Microsoft To Do task lists. The default list has the Tasks role.</summary>
    Task<IReadOnlyList<GraphExportFolder>> GetTaskListsAsync(CancellationToken cancellationToken);

    /// <summary>Returns one page of Graph To Do task resources from a task list.</summary>
    Task<GraphExportPage<JsonElement>> GetTasksAsync(string listId, string? pageLink, CancellationToken cancellationToken);
}

/// <summary>Standard mailbox folder role recognized by the exporter.</summary>
public enum GraphExportFolderRole {
    /// <summary>A user folder without a standard role.</summary>
    None = 0,
    /// <summary>Inbox.</summary>
    Inbox,
    /// <summary>Sent Items.</summary>
    SentItems,
    /// <summary>Drafts.</summary>
    Drafts,
    /// <summary>Deleted Items.</summary>
    DeletedItems,
    /// <summary>Junk Email.</summary>
    JunkEmail,
    /// <summary>Outbox.</summary>
    Outbox,
    /// <summary>Default contacts folder.</summary>
    Contacts,
    /// <summary>Default calendar.</summary>
    Calendar,
    /// <summary>Default task list.</summary>
    Tasks
}

/// <summary>One source folder, calendar or task list.</summary>
public sealed class GraphExportFolder {
    /// <summary>Creates a folder description.</summary>
    public GraphExportFolder(string id, string displayName, string? parentId = null,
        GraphExportFolderRole role = GraphExportFolderRole.None, long? totalItemCount = null) {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        ParentId = parentId;
        Role = role;
        TotalItemCount = totalItemCount;
    }

    /// <summary>Source identifier.</summary>
    public string Id { get; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; }

    /// <summary>Parent identifier, or null for a top-level folder.</summary>
    public string? ParentId { get; }

    /// <summary>Standard role, when the folder has one.</summary>
    public GraphExportFolderRole Role { get; }

    /// <summary>Item count reported by the source, when known.</summary>
    public long? TotalItemCount { get; }
}

/// <summary>Listing metadata for one message.</summary>
public sealed class GraphExportMessageInfo {
    /// <summary>Creates message listing metadata.</summary>
    public GraphExportMessageInfo(string id, DateTimeOffset? receivedDateTime = null, string? subject = null,
        bool? isRead = null, bool? isDraft = null, string? importance = null,
        IReadOnlyList<string>? categories = null, long? size = null) {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        ReceivedDateTime = receivedDateTime;
        Subject = subject;
        IsRead = isRead;
        IsDraft = isDraft;
        Importance = importance;
        Categories = categories ?? Array.Empty<string>();
        Size = size;
    }

    /// <summary>Source message identifier.</summary>
    public string Id { get; }

    /// <summary>Time the message was received.</summary>
    public DateTimeOffset? ReceivedDateTime { get; }

    /// <summary>Subject, used for progress and failure reports.</summary>
    public string? Subject { get; }

    /// <summary>Read state.</summary>
    public bool? IsRead { get; }

    /// <summary>Draft state.</summary>
    public bool? IsDraft { get; }

    /// <summary>Graph importance value: low, normal or high.</summary>
    public string? Importance { get; }

    /// <summary>Outlook categories.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>Message size in bytes, when the source reports it.</summary>
    public long? Size { get; }
}

/// <summary>One page of source results.</summary>
public sealed class GraphExportPage<T> {
    /// <summary>Creates a page.</summary>
    public GraphExportPage(IReadOnlyList<T> items, string? nextLink) {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        NextLink = string.IsNullOrEmpty(nextLink) ? null : nextLink;
    }

    /// <summary>Items on this page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Continuation link, or null on the last page.</summary>
    public string? NextLink { get; }
}
