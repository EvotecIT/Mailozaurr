namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Represents a Delivery Status Notification status code.
/// </summary>
public sealed class DsnStatus {
    /// <summary>Classification of the DSN status code.</summary>
    public DsnStatusClass Class { get; }

    /// <summary>Subject subcode of the DSN status.</summary>
    public int Subject { get; }

    /// <summary>Detail subcode of the DSN status.</summary>
    public int Detail { get; }

    private DsnStatus(DsnStatusClass statusClass, int subject, int detail) {
        Class = statusClass;
        Subject = subject;
        Detail = detail;
    }

    /// <summary>Parses a DSN status string (e.g. "5.1.1").</summary>
    public static DsnStatus Parse(string value) {
        if (!TryParse(value, out var status)) {
            throw new FormatException($"Invalid DSN status '{value}'.");
        }
        return status;
    }

    /// <summary>Attempts to parse a DSN status string.</summary>
    public static bool TryParse(string? value, out DsnStatus status) {
        status = null!;
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }
        var parts = value!.Trim().Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var cls) || !int.TryParse(parts[1], out var subj) || !int.TryParse(parts[2], out var detail)) {
            return false;
        }
        if (!Enum.IsDefined(typeof(DsnStatusClass), cls)) {
            return false;
        }
        status = new DsnStatus((DsnStatusClass)cls, subj, detail);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => $"{(int)Class}.{Subject}.{Detail}";
}