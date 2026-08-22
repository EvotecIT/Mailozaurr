using OfficeIMO.Email;
using System.Security.Cryptography;

namespace Mailozaurr;

/// <summary>Validates and atomically writes provider-supplied RFC 822 content through OfficeIMO.Email.</summary>
public sealed class ProviderEmlArtifactWriter {
    /// <summary>Writes one bounded provider message without regenerating its MIME representation.</summary>
    public async Task<ProviderEmlArtifactWriteResult> WriteAsync(
        byte[] content,
        string destinationPath,
        long maxBytes,
        bool overwrite = false,
        CancellationToken cancellationToken = default) {
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (content.LongLength > maxBytes) {
            throw new InvalidOperationException($"Provider message exceeds the configured {maxBytes} byte export limit.");
        }

        var readerOptions = new EmailReaderOptions(
            maxInputBytes: maxBytes,
            includeAttachmentContent: false,
            preserveRawSource: true);
        using var stream = new MemoryStream(content, writable: false);
        using EmailReadResult readResult = await new EmailDocumentReader(readerOptions)
            .ReadAsync(stream, "message.eml", cancellationToken)
            .ConfigureAwait(false);
        if (readResult.HasErrors) {
            throw CreateDiagnosticException("OfficeIMO.Email rejected the provider MIME content", readResult.Diagnostics);
        }

        var writerOptions = new EmailWriterOptions(
            usePreservedRawSource: true,
            maxOutputBytes: maxBytes);
        EmailWriteResult writeResult = await readResult.Document.SaveWithConflictPolicyAsync(
            destinationPath,
            EmailFileFormat.Eml,
            overwrite ? EmailFileConflictPolicy.Replace : EmailFileConflictPolicy.FailIfExists,
            writerOptions,
            cancellationToken)
            .ConfigureAwait(false);
        if (writeResult.HasErrors) {
            throw CreateDiagnosticException("OfficeIMO.Email could not write the provider MIME content", writeResult.Diagnostics);
        }
        if (!writeResult.UsedPreservedSource) {
            throw new InvalidDataException("OfficeIMO.Email did not use the preserved provider source for the EML export.");
        }

        using var sha256 = SHA256.Create();
        var digest = BitConverter.ToString(sha256.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
        return new ProviderEmlArtifactWriteResult {
            DestinationPath = Path.GetFullPath(destinationPath),
            BytesWritten = writeResult.BytesWritten,
            Sha256 = digest,
            UsedPreservedSource = writeResult.UsedPreservedSource,
            DiagnosticCodes = writeResult.Diagnostics.Select(item => item.Code).ToList()
        };
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

    /// <summary>Whether OfficeIMO.Email emitted the retained source bytes verbatim.</summary>
    public bool UsedPreservedSource { get; set; }

    /// <summary>Structured OfficeIMO.Email diagnostic codes.</summary>
    public List<string> DiagnosticCodes { get; set; } = new();
}
