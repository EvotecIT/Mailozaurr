namespace Mailozaurr;

/// <summary>
/// Represents a thread item returned by Gmail REST API.
/// </summary>
public sealed class GmailThreadInfo {
    /// <summary>Unique thread identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Thread history identifier.</summary>
    public string? HistoryId { get; set; }

    /// <summary>Thread snippet.</summary>
    public string? Snippet { get; set; }
}
