using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading;

namespace Mailozaurr;

public partial class Graph {
    private readonly Dictionary<Definitions.AttachmentDescriptor, long> _resolvedAttachmentLengths = new();

    /// <summary>
    /// Converts the <see cref="Attachments"/> collection into <see cref="GraphAttachment"/> instances.
    /// </summary>
    public void CreateAttachments() {
        ConvertedAttachments.Clear();
        AttachmentsPlaceHolders.Clear();
        _deferredGraphAttachments.Clear();
        TotalAttachmentSizeBytes = 0;
        RawAttachmentSizeBytes = 0;
        IsLargerAttachment = false;
        _inlineAttachmentSizeBytes = 0;
        _streamableAttachmentCount = 0;
        _convertedStreamableAttachmentStartIndex = -1;
        _resolvedAttachmentLengths.Clear();
        if (Attachments != null && Attachments.Any()) {
            var streamableAttachments = new List<GraphFileAttachmentSource>();
            var regularFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
            var inlineFilePaths = Definitions.AttachmentPathIdentity.CreateSet();
            long fileTotalBytes = 0;
            long inMemoryTotalBytes = 0;
            long rawAttachmentBytes = 0;

            // First pass: compute total size without loading file contents.
            foreach (var item in Attachments) {
                if (item is GraphAttachment ga) {
                    ConvertedAttachments.Add(ga);
                    var size = EstimateAttachmentSize(ga);
                    inMemoryTotalBytes += size;
                    rawAttachmentBytes += EstimateRawAttachmentSize(ga);
                } else if (item is Definitions.AttachmentDescriptor descriptor) {
                    if (descriptor.SourcePath is string descriptorPath) {
                        var seenPaths = IsInlineDescriptor(descriptor) ? inlineFilePaths : regularFilePaths;
                        TrackFileAttachment(descriptorPath, descriptor, streamableAttachments, seenPaths, ref fileTotalBytes, ref rawAttachmentBytes);
                    } else if (IsStreamableDescriptor(descriptor)) {
                        long descriptorLength = ResolveAttachmentLength(descriptor);
                        var source = new GraphFileAttachmentSource(descriptor, descriptorLength);
                        streamableAttachments.Add(source);
                        fileTotalBytes += EstimateStreamableAttachmentSize(source);
                        rawAttachmentBytes += descriptorLength;
                        _streamableAttachmentCount++;
                    } else {
                        var converted = GraphAttachment.FromDescriptor(descriptor, descriptor.Length);
                        ConvertedAttachments.Add(converted);
                        inMemoryTotalBytes += EstimateAttachmentSize(converted);
                        rawAttachmentBytes += EstimateRawAttachmentSize(converted);
                    }
                } else if (TryGetAttachmentPath(item, out var path)) {
                    TrackFileAttachment(path, null, streamableAttachments, regularFilePaths, ref fileTotalBytes, ref rawAttachmentBytes);
                }
            }

            _inlineAttachmentSizeBytes = inMemoryTotalBytes;
            TotalAttachmentSizeBytes = fileTotalBytes + inMemoryTotalBytes;
            RawAttachmentSizeBytes = rawAttachmentBytes;
            IsLargerAttachment = TotalAttachmentSizeBytes > GraphPayloadLimitBytes;

            // Only load file attachments into memory when they fit in a simple send payload.
            if (!IsLargerAttachment && streamableAttachments.Count > 0) {
                _convertedStreamableAttachmentStartIndex = ConvertedAttachments.Count;
                foreach (GraphFileAttachmentSource source in streamableAttachments) {
                    ConvertedAttachments.Add(source.Descriptor == null
                        ? GraphAttachment.FromFile(source.Path!)
                        : GraphAttachment.FromDescriptor(source.Descriptor, source.Length));
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
        ICollection<GraphFileAttachmentSource> fileAttachments,
        ISet<string> seenFilePaths,
        ref long fileTotalBytes,
        ref long rawAttachmentBytes) {
        if (!File.Exists(path)) {
            LogMissingAttachmentWarning(path);
            return;
        }

        try {
            if (!Definitions.AttachmentPathIdentity.Add(seenFilePaths, path)) {
                return;
            }
            var source = new GraphFileAttachmentSource(path, descriptor);
            fileAttachments.Add(source);
            fileTotalBytes += EstimateStreamableAttachmentSize(source);
            rawAttachmentBytes += source.Length;
            _streamableAttachmentCount++;
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
                    continue;
                }
                if (IsStreamableDescriptor(descriptor)) {
                    long descriptorLength = ResolveAttachmentLength(descriptor);
                    yield return new GraphFileAttachmentSource(descriptor, descriptorLength);
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

    private long ResolveAttachmentLength(Definitions.AttachmentDescriptor descriptor) {
        if (_resolvedAttachmentLengths.TryGetValue(descriptor, out long resolvedLength)) {
            return resolvedLength;
        }

        resolvedLength = descriptor.Length ?? descriptor.MeasureContentLength(
            Definitions.AttachmentStreamStagingOptions.DefaultMaxBytes);
        if (resolvedLength < 0 || resolvedLength > Definitions.AttachmentStreamStagingOptions.DefaultMaxBytes) {
            throw new InvalidDataException(
                $"Attachment content exceeds the {Definitions.AttachmentStreamStagingOptions.DefaultMaxBytes} byte Graph read limit.");
        }
        _resolvedAttachmentLengths.Add(descriptor, resolvedLength);
        return resolvedLength;
    }

    private static bool IsStreamableDescriptor(Definitions.AttachmentDescriptor descriptor) =>
        descriptor is Definitions.ContentSourceAttachmentDescriptor or Definitions.StreamAttachmentDescriptor;

    private sealed class GraphFileAttachmentSource {
        internal GraphFileAttachmentSource(
            string path,
            Definitions.AttachmentDescriptor? descriptor,
            long? length = null) {
            Path = path;
            Descriptor = descriptor;
            Length = length ?? (File.Exists(path) ? new FileInfo(path).Length : 0);
        }

        internal GraphFileAttachmentSource(Definitions.AttachmentDescriptor descriptor, long length) {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            Length = length >= 0 ? length : throw new ArgumentOutOfRangeException(nameof(length));
        }

        internal string? Path { get; }
        internal Definitions.AttachmentDescriptor? Descriptor { get; }
        internal long Length { get; }

        internal Task<Stream> OpenReadAsync(CancellationToken cancellationToken) {
            if (Descriptor != null) return Descriptor.OpenContentStreamAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Stream stream = new FileStream(Path!, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, bufferSize: 64 * 1024, useAsync: true);
            return Task.FromResult(stream);
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

        return EstimateSerializedAttachmentSize(
            value.Length,
            attachment.Name,
            attachment.ContentType,
            attachment.ContentId,
            attachment.ODataType);
    }

    private static long EstimateRawAttachmentSize(GraphAttachment attachment) {
        if (string.IsNullOrWhiteSpace(attachment.ContentBytes)) {
            return 0;
        }

        long encodedCharacters = 0;
        var paddingCharacters = 0;
        foreach (var character in attachment.ContentBytes) {
            if (char.IsWhiteSpace(character)) {
                continue;
            }
            encodedCharacters++;
            if (character == '=') {
                paddingCharacters++;
            }
        }

        return encodedCharacters == 0 || encodedCharacters % 4 != 0
            ? 0
            : encodedCharacters / 4 * 3 - Math.Min(paddingCharacters, 2);
    }

    private static long EstimateStreamableAttachmentSize(GraphFileAttachmentSource source) {
        var fileLength = source.Length;
        Definitions.AttachmentDescriptor? descriptor = source.Descriptor;
        var encodedLength = fileLength > (long.MaxValue - 2) / 4 * 3
            ? long.MaxValue
            : ((fileLength + 2) / 3) * 4;
        var fileName = string.IsNullOrWhiteSpace(descriptor?.FileName)
            ? Path.GetFileName(source.Path) ?? "attachment"
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
        if (_convertedStreamableAttachmentStartIndex < 0 || _streamableAttachmentCount <= 0 ||
            _convertedStreamableAttachmentStartIndex + _streamableAttachmentCount > ConvertedAttachments.Count) {
            return false;
        }

        ConvertedAttachments.RemoveRange(_convertedStreamableAttachmentStartIndex, _streamableAttachmentCount);
        _convertedStreamableAttachmentStartIndex = -1;
        IsLargerAttachment = true;
        MessageContainer.Message.Attachments = ConvertedAttachments.Count == 0 ? null : ConvertedAttachments;
        MessageJson = JsonSerializer.Serialize(MessageContainer, GraphJsonContext.Default.GraphMessageContainer);
        return true;
    }

    private bool TryRouteEligibleAttachments(Func<bool> exceedsPayloadLimit) {
        var changed = false;
        for (var index = ConvertedAttachments.Count - 1;
             index >= 0 && exceedsPayloadLimit();
             index--) {
            var attachment = ConvertedAttachments[index];
            ConvertedAttachments.RemoveAt(index);
            _deferredGraphAttachments.Insert(0, attachment);
            MessageContainer.Message.Attachments = ConvertedAttachments.Count == 0 ? null : ConvertedAttachments;
            MessageJson = JsonSerializer.Serialize(MessageContainer, GraphJsonContext.Default.GraphMessageContainer);
            changed = true;
        }

        if (changed) {
            IsLargerAttachment = true;
        }
        return changed;
    }
}
