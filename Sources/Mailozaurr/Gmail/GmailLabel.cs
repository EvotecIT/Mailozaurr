namespace Mailozaurr;

/// <summary>Gmail label returned by the Gmail API.</summary>
public sealed class GmailLabel {
    /// <summary>Label id (for system labels often equals the name).</summary>
    public string? Id { get; set; }

    /// <summary>Label name.</summary>
    public string? Name { get; set; }

    /// <summary>Label type (for example "system" or "user").</summary>
    public string? Type { get; set; }
}