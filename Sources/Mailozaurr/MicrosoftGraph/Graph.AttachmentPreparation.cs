using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    /// <summary>
    /// Converts the <see cref="Attachments"/> collection into <see cref="GraphAttachment"/> instances.
    /// </summary>
    public void CreateAttachments() {
        ConvertedAttachments.Clear();
        TotalAttachmentSizeBytes = 0;
        IsLargerAttachment = false;
        _inlineAttachmentSizeBytes = 0;
        _fileAttachmentCount = 0;
        if (Attachments != null && Attachments.Any()) {
            var fileAttachments = new List<KeyValuePair<string, Definitions.AttachmentDescriptor?>>();
            long fileTotalBytes = 0;
            long inMemoryTotalBytes = 0;

            // First pass: compute total size without loading file contents.
            foreach (var item in Attachments) {
                if (item is GraphAttachment ga) {
                    ConvertedAttachments.Add(ga);
                    var size = EstimateAttachmentSize(ga);
                    inMemoryTotalBytes += size;
                } else if (item is Definitions.AttachmentDescriptor descriptor) {
                    if (!IsInlineDescriptor(descriptor) && descriptor.SourcePath is string descriptorPath) {
                        TrackFileAttachment(descriptorPath, descriptor, fileAttachments, ref fileTotalBytes);
                    } else {
                        var converted = GraphAttachment.FromDescriptor(descriptor);
                        ConvertedAttachments.Add(converted);
                        inMemoryTotalBytes += EstimateAttachmentSize(converted);
                    }
                } else if (TryGetAttachmentPath(item, out var path)) {
                    TrackFileAttachment(path, null, fileAttachments, ref fileTotalBytes);
                }
            }

            _inlineAttachmentSizeBytes = inMemoryTotalBytes;
            TotalAttachmentSizeBytes = fileTotalBytes + inMemoryTotalBytes;
            IsLargerAttachment = TotalAttachmentSizeBytes > GraphPayloadLimitBytes;

            // Only load file attachments into memory when they fit in a simple send payload.
            if (!IsLargerAttachment && fileAttachments.Count > 0) {
                foreach (var source in fileAttachments) {
                    ConvertedAttachments.Add(source.Value == null
                        ? GraphAttachment.FromFile(source.Key)
                        : GraphAttachment.FromDescriptor(source.Value));
                }
            }

            if (_inlineAttachmentSizeBytes > GraphPayloadLimitBytes) {
                LogCollector.LogWarning("Send-EmailMessage - Large in-memory attachments detected. Consider using file paths for large attachments to enable upload sessions.");
            }
        }
    }

    private void TrackFileAttachment(
        string path,
        Definitions.AttachmentDescriptor? descriptor,
        ICollection<KeyValuePair<string, Definitions.AttachmentDescriptor?>> fileAttachments,
        ref long fileTotalBytes) {
        if (!File.Exists(path)) {
            LogMissingAttachmentWarning(path);
            return;
        }

        try {
            fileAttachments.Add(new KeyValuePair<string, Definitions.AttachmentDescriptor?>(path, descriptor));
            fileTotalBytes += new FileInfo(path).Length;
            _fileAttachmentCount++;
        } catch (Exception ex) {
            LogCollector.LogError($"Send-EmailMessage - Failed to read attachment '{path}': {ex.Message}");
        }
    }

    private static bool IsInlineDescriptor(Definitions.AttachmentDescriptor descriptor) =>
        string.Equals(
            descriptor.ContentDisposition?.Disposition,
            MimeKit.ContentDisposition.Inline,
            StringComparison.OrdinalIgnoreCase);

    private static bool TryGetAttachmentPath(object? item, out string path) {
        path = item switch {
            string value => value,
            FileInfo fileInfo => fileInfo.FullName,
            _ => item?.ToString() ?? string.Empty
        };

        return !string.IsNullOrWhiteSpace(path);
    }

    private IEnumerable<string> EnumerateAttachmentPaths() {
        if (Attachments == null || Attachments.Length == 0) {
            yield break;
        }

        foreach (var item in Attachments) {
            if (item is GraphAttachment) {
                continue;
            }
            if (item is Definitions.AttachmentDescriptor descriptor) {
                if (!IsInlineDescriptor(descriptor) && descriptor.SourcePath is string descriptorPath) {
                    yield return descriptorPath;
                }
                continue;
            }
            if (TryGetAttachmentPath(item, out var path)) {
                yield return path;
            }
        }
    }

    private static long EstimateTotalSize(IEnumerable<GraphAttachment> attachments) {
        long total = 0;
        foreach (var attachment in attachments) {
            total += EstimateAttachmentSize(attachment);
        }
        return total;
    }

    private static long EstimateAttachmentSize(GraphAttachment attachment) {
        if (string.IsNullOrWhiteSpace(attachment.ContentBytes)) {
            return 0;
        }
        var value = attachment.ContentBytes.Trim();
        if (value.Length == 0) {
            return 0;
        }
        var padding = 0;
        if (value.EndsWith("==", StringComparison.Ordinal)) {
            padding = 2;
        } else if (value.EndsWith("=", StringComparison.Ordinal)) {
            padding = 1;
        }
        var bytes = (long)value.Length * 3 / 4 - padding;
        return bytes < 0 ? 0 : bytes;
    }
}
