using MimeKit.Utils;

namespace Mailozaurr.NonDeliveryReports;

/// <summary>
/// Represents a parsed Non-Delivery Report (Delivery Status Notification).
/// </summary>
public sealed class NonDeliveryReport {
    /// <summary>Original recipient as specified in the NDR.</summary>
    public string? OriginalRecipient { get; set; }

    /// <summary>Normalized address extracted from <see cref="OriginalRecipient"/>.</summary>
    public string? OriginalRecipientAddress { get; set; }

    /// <summary>Final recipient that the report refers to.</summary>
    public string? FinalRecipient { get; set; }

    /// <summary>Normalized address extracted from <see cref="FinalRecipient"/>.</summary>
    public string? FinalRecipientAddress { get; set; }

    /// <summary>Identifier of the original message associated with this report.</summary>
    public string? OriginalMessageId { get; set; }

    /// <summary>Reporting mail transfer agent.</summary>
    public string? ReportingMta { get; set; }

    /// <summary>Action taken by the server for the delivery attempt.</summary>
    public string? Action { get; set; }

    /// <summary>Remote mail transfer agent involved in the delivery.</summary>
    public string? RemoteMta { get; set; }

    /// <summary>Date of the last delivery attempt.</summary>
    public DateTimeOffset? LastAttemptDate { get; set; }

    /// <summary>Identifier of the final log entry.</summary>
    public string? FinalLogId { get; set; }

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
        headers.TryGetValue("Action", out var action);
        headers.TryGetValue("Remote-MTA", out var remoteMta);
        headers.TryGetValue("Last-Attempt-Date", out var lastAttemptDate);
        headers.TryGetValue("Final-Log-ID", out var finalLogId);
        headers.TryGetValue("Original-Message-ID", out var originalMessageId);
        headers.TryGetValue("Diagnostic-Code", out var diagnosticCode);
        headers.TryGetValue("Status", out var statusCode);
        headers.TryGetValue("Arrival-Date", out var arrivalDate);

        var ndr = new NonDeliveryReport {
            OriginalRecipient = originalRecipient,
            OriginalRecipientAddress = ExtractAddress(originalRecipient),
            FinalRecipient = finalRecipient,
            FinalRecipientAddress = ExtractAddress(finalRecipient),
            OriginalMessageId = NormalizeMessageId(originalMessageId),
            ReportingMta = reportingMta,
            Action = action,
            RemoteMta = remoteMta,
            LastAttemptDate = TryParseTimestamp(lastAttemptDate),
            FinalLogId = finalLogId,
            DiagnosticCode = diagnosticCode is not null ? DsnDiagnosticCode.Parse(diagnosticCode) : null,
            Status = statusCode is not null && DsnStatus.TryParse(statusCode, out var status) ? status : null,
            Timestamp = TryParseTimestamp(arrivalDate) ?? TryParseTimestamp(lastAttemptDate) ?? DateTimeOffset.MinValue
        };
        return ndr;
    }

    private static string? ExtractAddress(string? header) {
        if (string.IsNullOrWhiteSpace(header)) {
            return null;
        }
        var idx = header!.IndexOf(';');
        return idx >= 0 ? header.Substring(idx + 1).Trim() : header.Trim();
    }

    internal static string? NormalizeMessageId(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        var trimmed = value!.Trim();
        if (trimmed.Length > 1 && trimmed[0] == '<' && trimmed[trimmed.Length - 1] == '>') {
            trimmed = trimmed.Substring(1, trimmed.Length - 2);
        }
        return trimmed;
    }

    private static DateTimeOffset? TryParseTimestamp(string? value)
        => DateUtils.TryParse(value, out var dt) ? dt : null;

    /// <summary>Maps a <see cref="DsnStatus"/> to an <see cref="NonDeliveryReportType"/>.</summary>
    public static NonDeliveryReportType GetReportType(DsnStatus? status) {
        if (status is null) {
            return NonDeliveryReportType.Unknown;
        }
        return status switch {
            { Subject: 1, Detail: 1 } => NonDeliveryReportType.UnknownRecipient,
            { Subject: 2, Detail: 2 } => NonDeliveryReportType.MailboxFull,
            { Subject: 7 } => NonDeliveryReportType.PolicyBlock,
            { Subject: 6 } => NonDeliveryReportType.ContentRejected,
            { Subject: 4 } => NonDeliveryReportType.DnsFailure,
            { Class: DsnStatusClass.PermanentFailure } => NonDeliveryReportType.HardBounce,
            { Class: DsnStatusClass.PersistentTransientFailure } => NonDeliveryReportType.SoftBounce,
            _ => NonDeliveryReportType.Unknown
        };
    }
}
