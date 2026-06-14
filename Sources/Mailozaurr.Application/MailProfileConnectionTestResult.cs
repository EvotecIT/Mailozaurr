namespace Mailozaurr.Application;

/// <summary>
/// Represents the outcome of a reusable provider connection test.
/// </summary>
public sealed class MailProfileConnectionTestResult : OperationResult {
    /// <summary>The tested profile identifier.</summary>
    public string? ProfileId { get; set; }

    /// <summary>The tested profile kind.</summary>
    public MailProfileKind ProfileKind { get; set; }

    /// <summary>The provider action used to verify connectivity.</summary>
    public string? Probe { get; set; }

    /// <summary>The resolved mailbox or account target used for the test, when applicable.</summary>
    public string? Target { get; set; }

    /// <summary>The requested test scope.</summary>
    public MailProfileConnectionTestScope RequestedScope { get; set; }

    /// <summary>The scope that was actually executed.</summary>
    public MailProfileConnectionTestScope ExecutedScope { get; set; }
}