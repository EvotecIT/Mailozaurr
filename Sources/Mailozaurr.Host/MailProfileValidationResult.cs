namespace Mailozaurr.Hosting;

/// <summary>
/// Represents the result of validating a mail profile definition.
/// </summary>
public sealed class MailProfileValidationResult : OperationResult {
    /// <summary>Validation errors.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Validation warnings.</summary>
    public List<string> Warnings { get; } = new();
}