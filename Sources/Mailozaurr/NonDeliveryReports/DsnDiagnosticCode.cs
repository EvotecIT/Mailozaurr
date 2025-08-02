namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Represents the diagnostic code from a Delivery Status Notification.
/// </summary>
public sealed class DsnDiagnosticCode {
    /// <summary>The diagnostic code type, e.g. "smtp".</summary>
    public string Type { get; }

    /// <summary>The textual diagnostic information.</summary>
    public string Text { get; }

    private DsnDiagnosticCode(string type, string text) {
        Type = type;
        Text = text;
    }

    /// <summary>Parses a diagnostic code string in the form "smtp; 550 5.1.1".</summary>
    public static DsnDiagnosticCode Parse(string value) {
        var parts = value.Split(';', 2);
        var type = parts[0].Trim();
        var text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return new DsnDiagnosticCode(type, text);
    }

    /// <inheritdoc />
    public override string ToString() => string.IsNullOrEmpty(Text) ? Type : $"{Type}; {Text}";
}
