using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Mailozaurr;

/// <summary>Resumes an explicit EML inventory and verifies retained files before skipping them.</summary>
public sealed class MailEmlArchiveService {
    private readonly IMailProfileStore _profiles;
    private readonly IMailEmlExportService _export;

    /// <summary>Creates an archive workflow over the normal provider export service.</summary>
    public MailEmlArchiveService(IMailProfileStore profiles, IMailEmlExportService export) {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _export = export ?? throw new ArgumentNullException(nameof(export));
    }

    /// <summary>Archives every selected ID, or resumes after interruption using verified records.</summary>
    public async Task<MailEmlArchiveResult> ArchiveAsync(
        MailEmlArchiveRequest request, CancellationToken cancellationToken = default) {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProfileId)) throw new ArgumentException("Profile ID is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DestinationDirectory)) throw new ArgumentException("Archive directory is required.", nameof(request));
        if (request.MessageIds == null || request.MessageIds.Count == 0) throw new ArgumentException("A complete message ID inventory is required.", nameof(request));
        if (request.MaxMessageBytes <= 0 || request.MaxMessageBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes));

        var profile = await _profiles.GetByIdAsync(request.ProfileId.Trim(), cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Profile '{request.ProfileId}' was not found.");
        if (profile.Kind == MailProfileKind.Imap && request.MaxMessageBytes == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.MaxMessageBytes),
                "IMAP archive limit must be below Int32.MaxValue so an extra byte can detect oversized content.");
        var ids = request.MessageIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => MailEmlExportService.CanonicalizeMessageIdForStorage(profile, id.Trim()))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) throw new ArgumentException("A complete message ID inventory is required.", nameof(request));

        var root = Path.GetFullPath(request.DestinationDirectory);
        var metadataDirectory = Path.Combine(root, ".mailozaurr-archive");
        var recordsDirectory = Path.Combine(metadataDirectory, "records");
        var emlDirectory = Path.Combine(root, "eml");
        EnsureRestrictedDirectory(root);
        EnsureRestrictedDirectory(metadataDirectory);
        // A second worker must not mutate the same archive while this run is verifying it.
        using var archiveLock = UnixFilePermissions.OpenRestrictedFile(Path.Combine(metadataDirectory, "lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (_export is not IMailEmlArchiveScopeProvider scopeProvider)
            throw new NotSupportedException("Archive export requires a provider scope aware export service.");
        var providerScope = await scopeProvider.GetArchiveScopeAsync(profile, request.MailboxId, request.FolderId,
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(providerScope))
            throw new InvalidOperationException("The provider did not return a mailbox identity scope.");
        var scopePath = Path.Combine(metadataDirectory, "scope.json");
        var expectedScope = new MailEmlArchiveScopeDocument {
            ProfileId = profile.Id,
            ProfileFingerprint = HashProfile(profile),
            ProviderScope = providerScope,
            MailboxId = request.MailboxId?.Trim(),
            FolderId = request.FolderId?.Trim(),
            InventorySha256 = HashInventory(ids),
            InventoryCount = ids.Count
        };
        if (File.Exists(scopePath)) {
            using var scopeStream = new FileStream(scopePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var actualScope = await JsonSerializer.DeserializeAsync(scopeStream,
                ApplicationJsonContext.Default.MailEmlArchiveScopeDocument, cancellationToken).ConfigureAwait(false);
            if (actualScope == null || actualScope.Version != 2 ||
                actualScope.ProfileId != expectedScope.ProfileId ||
                actualScope.ProfileFingerprint != expectedScope.ProfileFingerprint ||
                actualScope.ProviderScope != expectedScope.ProviderScope ||
                actualScope.MailboxId != expectedScope.MailboxId ||
                actualScope.FolderId != expectedScope.FolderId ||
                actualScope.InventoryCount != expectedScope.InventoryCount ||
                actualScope.InventorySha256 != expectedScope.InventorySha256) {
                throw new InvalidOperationException("The archive inventory or mailbox scope differs from its recorded snapshot.");
            }
        } else {
            if (Directory.Exists(emlDirectory) && Directory.EnumerateFileSystemEntries(emlDirectory).Any()) {
                throw new InvalidOperationException("An existing EML directory has no archive scope record.");
            }
            await WriteRecordAsync(scopePath,
                (stream, token) => JsonSerializer.SerializeAsync(stream, expectedScope,
                    ApplicationJsonContext.Default.MailEmlArchiveScopeDocument, token),
                cancellationToken).ConfigureAwait(false);
        }
        EnsureRestrictedDirectory(recordsDirectory);
        EnsureRestrictedDirectory(emlDirectory);

        var result = new MailEmlArchiveResult {
            DestinationDirectory = root,
            RequestedCount = ids.Count
        };
        var pending = new List<string>(MailEmlExportService.MaximumBatchMessageCount);
        IMailEmlArchiveBatchSession? batchSession = null;
        try {
            foreach (var id in ids) {
                cancellationToken.ThrowIfCancellationRequested();
                var recordPath = GetRecordPath(recordsDirectory, id);
                if (await VerifyRecordAsync(recordPath, emlDirectory, id, cancellationToken).ConfigureAwait(false)) {
                    result.VerifiedExistingCount++;
                    continue;
                }
                pending.Add(id);
                if (pending.Count == MailEmlExportService.MaximumBatchMessageCount) {
                    if (batchSession == null && _export is MailEmlExportService defaultExport)
                        batchSession = await defaultExport.OpenArchiveSessionAsync(profile, cancellationToken).ConfigureAwait(false);
                    await ExportBatchAsync(request, profile.Id, providerScope,
                        emlDirectory, recordsDirectory, pending, batchSession,
                        result, cancellationToken).ConfigureAwait(false);
                    pending.Clear();
                }
            }
            if (pending.Count > 0) {
                if (batchSession == null && _export is MailEmlExportService defaultExport)
                    batchSession = await defaultExport.OpenArchiveSessionAsync(profile, cancellationToken).ConfigureAwait(false);
                await ExportBatchAsync(request, profile.Id, providerScope,
                    emlDirectory, recordsDirectory, pending, batchSession,
                    result, cancellationToken).ConfigureAwait(false);
            }
        } finally {
            batchSession?.Dispose();
        }
        result.Succeeded = result.FailedCount == 0 &&
            result.ExportedCount + result.VerifiedExistingCount == result.RequestedCount;
        result.Code = result.Succeeded ? null : "eml_archive_incomplete";
        result.Message = $"Archived {result.ExportedCount} new and verified {result.VerifiedExistingCount} existing EML file(s); {result.FailedCount} failed.";
        return result;
    }

    private async Task ExportBatchAsync(MailEmlArchiveRequest request, string profileId, string? providerScope,
        string emlDirectory, string recordsDirectory, List<string> ids,
        IMailEmlArchiveBatchSession? batchSession,
        MailEmlArchiveResult result, CancellationToken cancellationToken) {
        MailEmlExportResult exported;
        try {
            var exportRequest = new MailEmlExportRequest {
                ProfileId = profileId,
                MailboxId = request.MailboxId,
                FolderId = request.FolderId,
                MessageIds = new List<string>(ids),
                DestinationDirectory = emlDirectory,
                MaxMessageBytes = request.MaxMessageBytes,
                ExpectedProviderScope = providerScope,
                Overwrite = true
            };
            exported = batchSession == null
                ? await _export.ExportAsync(exportRequest, cancellationToken).ConfigureAwait(false)
                : await batchSession.ExportAsync(exportRequest, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            foreach (var id in ids) AddFailure(result, id, "eml_export_failed", ex.Message);
            return;
        }
        var items = exported.Results.ToDictionary(item => item.MessageId, StringComparer.Ordinal);
        foreach (var id in ids) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!items.TryGetValue(id, out var item) || !item.Succeeded ||
                string.IsNullOrWhiteSpace(item.DestinationPath) ||
                string.IsNullOrWhiteSpace(item.Sha256)) {
                AddFailure(result, id, item?.Code ?? "eml_export_failed", item?.Message);
                continue;
            }
            try {
                var path = Path.GetFullPath(item.DestinationPath);
                if (!string.Equals(Path.GetDirectoryName(path), emlDirectory,
                    OperatingSystemIsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                    throw new InvalidDataException("Exported file lies outside the archive EML directory.");
                }
                var fileName = Path.GetFileName(path);
                if (!await VerifyFileAsync(path, item.BytesWritten, item.Sha256!, cancellationToken).ConfigureAwait(false)) {
                    throw new InvalidDataException("Exported EML file failed its size or SHA-256 verification.");
                }
                var record = new MailEmlArchiveRecordDocument {
                    MessageId = id,
                    FileName = fileName,
                    Bytes = item.BytesWritten,
                    Sha256 = item.Sha256!
                };
                await WriteRecordAsync(GetRecordPath(recordsDirectory, id),
                    (stream, token) => JsonSerializer.SerializeAsync(stream, record,
                        ApplicationJsonContext.Default.MailEmlArchiveRecordDocument, token),
                    cancellationToken).ConfigureAwait(false);
                result.ExportedCount++;
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                AddFailure(result, id, "eml_archive_record_failed", ex.Message);
            }
        }
    }

    private static async Task<bool> VerifyRecordAsync(string recordPath, string emlDirectory,
        string id, CancellationToken cancellationToken) {
        if (!File.Exists(recordPath)) return false;
        try {
            using var stream = new FileStream(recordPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var record = await JsonSerializer.DeserializeAsync(stream,
                ApplicationJsonContext.Default.MailEmlArchiveRecordDocument, cancellationToken).ConfigureAwait(false);
            if (record == null || record.Version != 1 || record.MessageId != id ||
                string.IsNullOrWhiteSpace(record.FileName) ||
                record.FileName != Path.GetFileName(record.FileName) ||
                record.Bytes < 0 || string.IsNullOrWhiteSpace(record.Sha256)) return false;
            return await VerifyFileAsync(Path.Combine(emlDirectory, record.FileName),
                record.Bytes, record.Sha256, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (IOException) {
            return false;
        } catch (JsonException) {
            return false;
        }
    }

    private static async Task<bool> VerifyFileAsync(string path, long expectedBytes,
        string expectedHash, CancellationToken cancellationToken) {
        if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return false;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != expectedBytes) return false;
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0) {
            sha.TransformBlock(buffer, 0, read, buffer, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var actual = BitConverter.ToString(sha.Hash!).Replace("-", string.Empty).ToLowerInvariant();
        return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRecordPath(string recordsDirectory, string id) =>
        Path.Combine(recordsDirectory, HashText(id) + ".json");

    private static string HashInventory(IEnumerable<string> ids) {
        using var sha = SHA256.Create();
        foreach (var id in ids) {
            var bytes = Encoding.UTF8.GetBytes(id);
            var length = BitConverter.GetBytes(bytes.Length);
            sha.TransformBlock(length, 0, length.Length, length, 0);
            sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(sha.Hash!).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string HashText(string text) {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string HashProfile(MailProfile profile) {
        var builder = new StringBuilder();
        Append(profile.Id);
        Append(profile.Kind.ToString());
        Append(profile.DefaultMailbox);
        Append(profile.DefaultSender);
        foreach (var setting in profile.Settings.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)) {
            Append(setting.Key);
            Append(setting.Value);
        }
        return HashText(builder.ToString());

        void Append(string? value) {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value);
        }
    }

    private static void AddFailure(MailEmlArchiveResult result, string id, string? code, string? message) {
        result.FailedCount++;
        result.Failures.Add(new MailEmlArchiveFailure {
            MessageId = id,
            Code = code,
            Message = message
        });
    }

    private static bool OperatingSystemIsWindows() =>
        System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows);

    private static void EnsureRestrictedDirectory(string path) {
        var created = !Directory.Exists(path);
        Directory.CreateDirectory(path);
        if (created) UnixFilePermissions.RestrictDirectory(path);
    }

    private static async Task WriteRecordAsync(string path,
        Func<Stream, CancellationToken, Task> serialize, CancellationToken cancellationToken) {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Archive record has no parent directory.");
        var temporaryPath = Path.Combine(directory, ".record-" + Guid.NewGuid().ToString("N") + ".tmp");
        try {
            using (var stream = UnixFilePermissions.OpenRestrictedFile(temporaryPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                await serialize(stream, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporaryPath, path, null);
            else File.Move(temporaryPath, path);
        } finally {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

internal sealed class MailEmlArchiveScopeDocument {
    public int Version { get; set; } = 2;
    public string ProfileId { get; set; } = string.Empty;
    public string ProfileFingerprint { get; set; } = string.Empty;
    public string ProviderScope { get; set; } = string.Empty;
    public string? MailboxId { get; set; }
    public string? FolderId { get; set; }
    public string InventorySha256 { get; set; } = string.Empty;
    public int InventoryCount { get; set; }
}

internal sealed class MailEmlArchiveRecordDocument {
    public int Version { get; set; } = 1;
    public string MessageId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Bytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}
