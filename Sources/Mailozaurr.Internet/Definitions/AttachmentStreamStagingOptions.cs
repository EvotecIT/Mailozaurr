namespace Mailozaurr.Definitions;

/// <summary>Limits legacy one-shot stream materialization into reopenable attachment content.</summary>
public sealed class AttachmentStreamStagingOptions {
    /// <summary>Maximum bytes retained in memory before staging switches to a private temporary file.</summary>
    public long MemoryThresholdBytes { get; set; } = 4L * 1024 * 1024;

    /// <summary>Maximum bytes accepted from the source stream.</summary>
    public long MaxBytes { get; set; } = 1024L * 1024 * 1024;

    /// <summary>Optional directory for staged files. A private Mailozaurr temp directory is used by default.</summary>
    public string? TempDirectory { get; set; }

    internal AttachmentStreamStagingOptions CloneAndValidate() {
        if (MemoryThresholdBytes < 0) throw new ArgumentOutOfRangeException(nameof(MemoryThresholdBytes));
        if (MaxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxBytes));
        if (MemoryThresholdBytes > MaxBytes) throw new ArgumentException(
            "The memory threshold cannot exceed the maximum attachment size.", nameof(MemoryThresholdBytes));
        return new AttachmentStreamStagingOptions {
            MemoryThresholdBytes = MemoryThresholdBytes,
            MaxBytes = MaxBytes,
            TempDirectory = TempDirectory
        };
    }
}
