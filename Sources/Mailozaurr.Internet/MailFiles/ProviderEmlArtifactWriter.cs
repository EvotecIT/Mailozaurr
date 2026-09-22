using OfficeIMO.Email;
using System.Security.Cryptography;

namespace Mailozaurr;

/// <summary>Validates and atomically writes provider-supplied RFC 822 content through OfficeIMO.Email.</summary>
public sealed class ProviderEmlArtifactWriter {
    /// <summary>Writes one bounded provider message without regenerating its MIME representation.</summary>
    public Task<ProviderEmlArtifactWriteResult> WriteAsync(
        byte[] content,
        string destinationPath,
        long maxBytes,
        bool overwrite = false,
        CancellationToken cancellationToken = default) {
        if (content == null) throw new ArgumentNullException(nameof(content));
        return WriteBytesAsync(content, destinationPath, maxBytes, overwrite, cancellationToken);
    }

    private async Task<ProviderEmlArtifactWriteResult> WriteBytesAsync(byte[] content,
        string destinationPath, long maxBytes, bool overwrite, CancellationToken cancellationToken) {
        using var stream = new MemoryStream(content, writable: false);
        return await WriteAsync(stream, destinationPath, maxBytes, overwrite, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Validates and writes a provider stream with bounded working memory.</summary>
    public async Task<ProviderEmlArtifactWriteResult> WriteAsync(
        Stream content,
        string destinationPath,
        long maxBytes,
        bool overwrite = false,
        CancellationToken cancellationToken = default) {
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (!content.CanRead) throw new ArgumentException("The provider content must be readable.", nameof(content));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        string? stagedPath = null;
        Stream? stagedStream = null;
        Stream source = content;
        try {
            if (!content.CanSeek) {
                stagedPath = Path.Combine(Path.GetTempPath(), "mailozaurr-eml-" + Guid.NewGuid().ToString("N") + ".tmp");
                stagedStream = UnixFilePermissions.OpenRestrictedFile(stagedPath, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.None);
                await CopyBoundedAsync(content, stagedStream, maxBytes, null, cancellationToken).ConfigureAwait(false);
                stagedStream.Position = 0;
                source = stagedStream;
            }
            if (source.Length > maxBytes) {
                throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
            }
            source.Position = 0;

            var readerOptions = new EmailReaderOptions(
                maxInputBytes: maxBytes,
                includeAttachmentContent: false);
            using EmailReadResult readResult = await new EmailDocumentReader(readerOptions)
                .ReadStreamingAsync(source, "message.eml", cancellationToken).ConfigureAwait(false);
            if (readResult.HasErrors) {
                throw CreateDiagnosticException("OfficeIMO.Email rejected the provider MIME content", readResult.Diagnostics);
            }

            using var sha256 = SHA256.Create();
            long bytesWritten = 0;
            await AtomicEmlFileCopy.WriteAsync(destinationPath,
                async (destination, token) => {
                    source.Position = 0;
                    bytesWritten = await CopyBoundedAsync(source, destination, maxBytes, sha256, token)
                        .ConfigureAwait(false);
                },
                overwrite, cancellationToken).ConfigureAwait(false);
            var digest = BitConverter.ToString(sha256.Hash!).Replace("-", string.Empty).ToLowerInvariant();
            return new ProviderEmlArtifactWriteResult {
                DestinationPath = Path.GetFullPath(destinationPath),
                BytesWritten = bytesWritten,
                Sha256 = digest,
                UsedPreservedSource = true,
                DiagnosticCodes = readResult.Diagnostics.Select(item => item.Code).ToList()
            };
        } finally {
            if (stagedPath != null) {
                stagedStream?.Dispose();
                File.Delete(stagedPath);
            }
        }
    }

    private static async Task<long> CopyBoundedAsync(Stream source, Stream destination, long maxBytes,
        HashAlgorithm? hash, CancellationToken cancellationToken) {
        var buffer = new byte[81920];
        long total = 0;
        while (true) {
            var read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (total > maxBytes - read) {
                throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
            }
            hash?.TransformBlock(buffer, 0, read, buffer, 0);
            await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            total += read;
        }
        hash?.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return total;
    }

    private static InvalidDataException CreateDiagnosticException(
        string message,
        IReadOnlyList<EmailDiagnostic> diagnostics) {
        var error = diagnostics.FirstOrDefault(item => item.Severity == EmailDiagnosticSeverity.Error);
        return error == null
            ? new InvalidDataException(message + ".")
            : new InvalidDataException($"{message}: {error.Code}: {error.Message}");
    }
}

/// <summary>Evidence returned after a lossless OfficeIMO.Email write.</summary>
public sealed class ProviderEmlArtifactWriteResult {
    /// <summary>Full destination path.</summary>
    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Number of bytes written.</summary>
    public long BytesWritten { get; set; }

    /// <summary>SHA-256 digest of the provider bytes.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Whether the validated provider bytes were emitted verbatim.</summary>
    public bool UsedPreservedSource { get; set; }

    /// <summary>Structured OfficeIMO.Email diagnostic codes.</summary>
    public List<string> DiagnosticCodes { get; set; } = new();
}
