namespace Mailozaurr.DmarcReports;

/// <summary>
/// Defines resource limits applied while validating and inspecting DMARC report attachments.
/// </summary>
public sealed class DmarcReportInspectionOptions {
    /// <summary>Default maximum expanded bytes inspected for one attachment.</summary>
    public const long DefaultMaxUncompressedBytesPerAttachment = 10L * 1024 * 1024;

    /// <summary>Default maximum expanded bytes inspected for one search operation.</summary>
    public const long DefaultMaxTotalUncompressedBytes = 100L * 1024 * 1024;

    /// <summary>Default maximum number of report attachments accepted from one message.</summary>
    public const int DefaultMaxAttachmentsPerMessage = 25;

    /// <summary>Default maximum number of entries accepted in one ZIP attachment.</summary>
    public const int DefaultMaxArchiveEntriesPerAttachment = 100;

    /// <summary>Maximum expanded bytes inspected for one attachment.</summary>
    public long MaxUncompressedBytesPerAttachment { get; set; } = DefaultMaxUncompressedBytesPerAttachment;

    /// <summary>Maximum expanded bytes inspected across the complete search operation.</summary>
    public long MaxTotalUncompressedBytes { get; set; } = DefaultMaxTotalUncompressedBytes;

    /// <summary>Maximum number of DMARC report attachments accepted from one message.</summary>
    public int MaxAttachmentsPerMessage { get; set; } = DefaultMaxAttachmentsPerMessage;

    /// <summary>Maximum number of entries accepted in one ZIP attachment.</summary>
    public int MaxArchiveEntriesPerAttachment { get; set; } = DefaultMaxArchiveEntriesPerAttachment;

    internal DmarcReportInspectionPolicy CreatePolicy() {
        if (MaxUncompressedBytesPerAttachment <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxUncompressedBytesPerAttachment));
        }
        if (MaxTotalUncompressedBytes <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxTotalUncompressedBytes));
        }
        if (MaxTotalUncompressedBytes < MaxUncompressedBytesPerAttachment) {
            throw new ArgumentException(
                $"{nameof(MaxTotalUncompressedBytes)} must be greater than or equal to {nameof(MaxUncompressedBytesPerAttachment)}.",
                nameof(MaxTotalUncompressedBytes));
        }
        if (MaxAttachmentsPerMessage <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxAttachmentsPerMessage));
        }
        if (MaxArchiveEntriesPerAttachment <= 0) {
            throw new ArgumentOutOfRangeException(nameof(MaxArchiveEntriesPerAttachment));
        }

        return new DmarcReportInspectionPolicy(
            MaxUncompressedBytesPerAttachment,
            MaxTotalUncompressedBytes,
            MaxAttachmentsPerMessage,
            MaxArchiveEntriesPerAttachment);
    }

    internal static DmarcReportInspectionOptions FromLegacyLimit(long maxUncompressedSize) {
        if (maxUncompressedSize <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxUncompressedSize));
        }

        return new DmarcReportInspectionOptions {
            MaxUncompressedBytesPerAttachment = maxUncompressedSize,
            MaxTotalUncompressedBytes = Math.Max(DefaultMaxTotalUncompressedBytes, maxUncompressedSize)
        };
    }
}

internal readonly struct DmarcReportInspectionPolicy {
    internal DmarcReportInspectionPolicy(
        long maxUncompressedBytesPerAttachment,
        long maxTotalUncompressedBytes,
        int maxAttachmentsPerMessage,
        int maxArchiveEntriesPerAttachment) {
        MaxUncompressedBytesPerAttachment = maxUncompressedBytesPerAttachment;
        MaxTotalUncompressedBytes = maxTotalUncompressedBytes;
        MaxAttachmentsPerMessage = maxAttachmentsPerMessage;
        MaxArchiveEntriesPerAttachment = maxArchiveEntriesPerAttachment;
    }

    internal long MaxUncompressedBytesPerAttachment { get; }
    internal long MaxTotalUncompressedBytes { get; }
    internal int MaxAttachmentsPerMessage { get; }
    internal int MaxArchiveEntriesPerAttachment { get; }
}
