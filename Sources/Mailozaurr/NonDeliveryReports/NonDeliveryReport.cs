using System.Globalization;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Represents a parsed Non-Delivery Report (Delivery Status Notification).
/// </summary>
public sealed class NonDeliveryReport {
    /// <summary>Original recipient as specified in the NDR.</summary>
    public string? OriginalRecipient { get; set; }

    /// <summary>Final recipient that the report refers to.</summary>
    public string? FinalRecipient { get; set; }

    /// <summary>Reporting mail transfer agent.</summary>
    public string? ReportingMta { get; set; }

    /// <summary>Diagnostic code explaining the failure.</summary>
    public DsnDiagnosticCode? DiagnosticCode { get; set; }

    /// <summary>Status code for the delivery attempt.</summary>
    public DsnStatus? Status { get; set; }

    /// <summary>The time the message originally arrived at the reporting MTA.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Determines the NDR type derived from the status code.</summary>
    public NonDeliveryReportType Type => GetReportType(Status);

    /// <summary>Creates a <see cref="NonDeliveryReport"/> instance from DSN headers.</summary>
    public static NonDeliveryReport FromHeaders(IDictionary<string, string> headers) {
        headers.TryGetValue("Original-Recipient", out var originalRecipient);
        headers.TryGetValue("Final-Recipient", out var finalRecipient);
        headers.TryGetValue("Reporting-MTA", out var reportingMta);
        headers.TryGetValue("Diagnostic-Code", out var diagnosticCode);
        headers.TryGetValue("Status", out var statusCode);
        headers.TryGetValue("Arrival-Date", out var arrivalDate);

        var ndr = new NonDeliveryReport {
            OriginalRecipient = originalRecipient,
            FinalRecipient = finalRecipient,
            ReportingMta = reportingMta,
            DiagnosticCode = diagnosticCode is not null ? DsnDiagnosticCode.Parse(diagnosticCode) : null,
            Status = statusCode is not null && DsnStatus.TryParse(statusCode, out var status) ? status : null,
            Timestamp = ParseTimestamp(arrivalDate)
        };
        return ndr;
    }

    private static DateTimeOffset ParseTimestamp(string? value) {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) {
            return dt;
        }
        return DateTimeOffset.MinValue;
    }

    /// <summary>Maps a <see cref="DsnStatus"/> to an <see cref="NonDeliveryReportType"/>.</summary>
    public static NonDeliveryReportType GetReportType(DsnStatus? status) {
        if (status is null) {
            return NonDeliveryReportType.Unknown;
        }
        var statusCode = status.ToString();
        if (statusCode == "5.1.1") {
            return NonDeliveryReportType.UnknownRecipient;
        }
        if (statusCode == "5.2.2" || statusCode == "4.2.2") {
            return NonDeliveryReportType.MailboxFull;
        }
        return status.Class switch {
            DsnStatusClass.PermanentFailure => NonDeliveryReportType.HardBounce,
            DsnStatusClass.PersistentTransientFailure => NonDeliveryReportType.SoftBounce,
            _ => NonDeliveryReportType.Unknown
        };
    }
}