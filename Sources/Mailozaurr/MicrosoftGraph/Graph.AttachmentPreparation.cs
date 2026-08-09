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
        _convertedFileAttachmentStartIndex = -1;
        if (Attachments != null && Attachments.Any()) {
            var fileAttachments = new List<KeyValuePair<string, Definitions.AttachmentDescriptor?>>();
            var regularFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
            var inlineFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
            long fileTotalBytes = 0;
            long inMemoryTotalBytes = 0;

            // First pass: compute total size without loading file contents.
            foreach (var item in Attachments) {
                if (item is GraphAttachment ga) {
                    ConvertedAttachments.Add(ga);
                    var size = EstimateAttachmentSize(ga);
                    inMemoryTotalBytes += size;
                } else if (item is Definitions.AttachmentDescriptor descriptor) {
                    if (descriptor.SourcePath is string descriptorPath) {
                        var seenPaths = IsInlineDescriptor(descriptor) ? inlineFilePaths : regularFilePaths;
                        TrackFileAttachment(descriptorPath, descriptor, fileAttachments, seenPaths, ref fileTotalBytes);
                    } else {
                        var converted = GraphAttachment.FromDescriptor(descriptor);
                        ConvertedAttachments.Add(converted);
                        inMemoryTotalBytes += EstimateAttachmentSize(converted);
                    }
                } else if (TryGetAttachmentPath(item, out var path)) {
                    TrackFileAttachment(path, null, fileAttachments, regularFilePaths, ref fileTotalBytes);
                }
            }

            _inlineAttachmentSizeBytes = inMemoryTotalBytes;
            TotalAttachmentSizeBytes = fileTotalBytes + inMemoryTotalBytes;
            IsLargerAttachment = TotalAttachmentSizeBytes > GraphPayloadLimitBytes;

            // Only load file attachments into memory when they fit in a simple send payload.
            if (!IsLargerAttachment && fileAttachments.Count > 0) {
                _convertedFileAttachmentStartIndex = ConvertedAttachments.Count;
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
        ISet<string> seenFilePaths,
        ref long fileTotalBytes) {
        if (!File.Exists(path)) {
            LogMissingAttachmentWarning(path);
            return;
        }

        try {
            if (!Definitions.AttachmentPathIdentity.Add(seenFilePaths, path)) {
                return;
            }
            fileAttachments.Add(new KeyValuePair<string, Definitions.AttachmentDescriptor?>(path, descriptor));
            fileTotalBytes += EstimateFileAttachmentSize(path, descriptor);
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

    private IEnumerable<GraphFileAttachmentSource> EnumerateFileAttachmentSources() {
        if (Attachments == null || Attachments.Length == 0) {
            yield break;
        }

        var regularFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
        var inlineFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
        foreach (var item in Attachments) {
            if (item is GraphAttachment) {
                continue;
            }
            if (item is Definitions.AttachmentDescriptor descriptor) {
                if (descriptor.SourcePath is string descriptorPath) {
                    var seenPaths = IsInlineDescriptor(descriptor) ? inlineFilePaths : regularFilePaths;
                    if (Definitions.AttachmentPathIdentity.Add(seenPaths, descriptorPath)) {
                        yield return new GraphFileAttachmentSource(descriptorPath, descriptor);
                    }
                }
                continue;
            }
            if (TryGetAttachmentPath(item, out var path)) {
                if (Definitions.AttachmentPathIdentity.Add(regularFilePaths, path)) {
                    yield return new GraphFileAttachmentSource(path, descriptor: null);
                }
            }
        }
    }

    private sealed class GraphFileAttachmentSource {
        internal GraphFileAttachmentSource(string path, Definitions.AttachmentDescriptor? descriptor) {
            Path = path;
            Descriptor = descriptor;
        }

        internal string Path { get; }
        internal Definitions.AttachmentDescriptor? Descriptor { get; }
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

        return EstimateSerializedAttachmentSize(
            value.Length,
            attachment.Name,
            attachment.ContentType,
            attachment.ContentId,
            attachment.ODataType);
    }

    private static long EstimateFileAttachmentSize(string path, Definitions.AttachmentDescriptor? descriptor) {
        var fileLength = new FileInfo(path).Length;
        var encodedLength = fileLength > (long.MaxValue - 2) / 4 * 3
            ? long.MaxValue
            : ((fileLength + 2) / 3) * 4;
        var fileName = string.IsNullOrWhiteSpace(descriptor?.FileName)
            ? Path.GetFileName(path)
            : descriptor!.FileName!;
        var contentType = string.IsNullOrWhiteSpace(descriptor?.ContentType)
            ? MimeKit.MimeTypes.GetMimeType(fileName)
            : descriptor!.ContentType;
        var isInline = descriptor != null && IsInlineDescriptor(descriptor);
        var contentId = string.IsNullOrWhiteSpace(descriptor?.ContentId)
            ? (isInline ? fileName : null)
            : descriptor!.ContentId;
        return EstimateSerializedAttachmentSize(
            encodedLength,
            fileName,
            contentType,
            contentId,
            "#microsoft.graph.fileAttachment");
    }

    private static long EstimateSerializedAttachmentSize(
        long encodedContentLength,
        string? name,
        string? contentType,
        string? contentId,
        string? oDataType) {
        if (encodedContentLength == long.MaxValue) {
            return long.MaxValue;
        }

        // Include JSON names, punctuation, boolean fields, and the escaped UTF-8 metadata.
        const long jsonStructuralOverhead = 128;
        return encodedContentLength + jsonStructuralOverhead +
               EstimateJsonStringLength(name) +
               EstimateJsonStringLength(contentType) +
               EstimateJsonStringLength(contentId) +
               EstimateJsonStringLength(oDataType);
    }

    private static int EstimateJsonStringLength(string? value) =>
        string.IsNullOrEmpty(value)
            ? 0
            : System.Text.Json.JsonEncodedText.Encode(value!).EncodedUtf8Bytes.Length;

    private bool TryRouteConvertedFileAttachmentsThroughUploadSession() {
        if (_convertedFileAttachmentStartIndex < 0 || _fileAttachmentCount <= 0 ||
            _convertedFileAttachmentStartIndex + _fileAttachmentCount > ConvertedAttachments.Count) {
            return false;
        }

        ConvertedAttachments.RemoveRange(_convertedFileAttachmentStartIndex, _fileAttachmentCount);
        _convertedFileAttachmentStartIndex = -1;
        IsLargerAttachment = true;
        MessageContainer.Message.Attachments = ConvertedAttachments.Count == 0 ? null : ConvertedAttachments;
        MessageJson = JsonSerializer.Serialize(MessageContainer, MailozaurrJsonContext.Default.GraphMessageContainer);
        return true;
    }
}
