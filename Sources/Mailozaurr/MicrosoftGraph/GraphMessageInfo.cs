namespace Mailozaurr;

/// <summary>
/// Represents summary information about an email message returned from a search query.
/// </summary>
public class GraphMessageInfo {
    /// <summary>User principal name of the mailbox containing the message.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>Unique identifier of the message.</summary>
    public string? Id { get; set; }

    /// <summary>Subject of the message.</summary>
    public string? Subject { get; set; }

    /// <summary>Snippet extracted by the search service highlighting the match.</summary>
    public string? Summary { get; set; }
}
