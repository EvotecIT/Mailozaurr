namespace Mailozaurr;

/// <summary>
/// Represents a normalized recipient or sender identity.
/// </summary>
public sealed class MessageRecipient {
    /// <summary>Display name of the recipient.</summary>
    public string? Name { get; set; }

    /// <summary>Email address of the recipient.</summary>
    public string Address { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Address : $"{Name} <{Address}>";
}