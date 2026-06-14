namespace Mailozaurr.Application;

/// <summary>
/// Represents the result of an application-layer operation.
/// </summary>
public class OperationResult {
    /// <summary>Whether the operation completed successfully.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Stable machine-readable code when available.</summary>
    public string? Code { get; set; }

    /// <summary>Human-readable message for logs or UI surfaces.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static OperationResult Success(string? message = null) => new() {
        Succeeded = true,
        Message = message
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static OperationResult Failure(string code, string? message = null) => new() {
        Succeeded = false,
        Code = code,
        Message = message
    };
}