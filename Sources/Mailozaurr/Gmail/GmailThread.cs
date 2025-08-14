using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Represents a thread returned by Gmail REST API.
/// </summary>
public sealed class GmailThread {
    /// <summary>Unique thread identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Thread history identifier.</summary>
    public string? HistoryId { get; set; }

    /// <summary>Thread snippet.</summary>
    public string? Snippet { get; set; }

    /// <summary>Messages contained in this thread.</summary>
    public IList<GmailMessage>? Messages { get; set; }
}
