using System.Globalization;
using System.Text.Json;
using OfficeIMO.Email.Store;

namespace Mailozaurr.MailboxExport;

public sealed partial class GraphMailboxPstExporter {
    private GraphMailboxPstCheckpoint? _checkpoint;
    private bool _writerFinalizing;

    private string? CheckpointPath => _options.CheckpointPath == null ? null : Path.GetFullPath(_options.CheckpointPath);
    private static StringComparison PathComparison => Path.DirectorySeparatorChar == '\\'
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private string PartPath(int number) => _options.Split == null
        ? Path.GetFullPath(_options.DestinationPath) : GetPartPath(_options.DestinationPath, number);

    private string WorkingDirectory(GraphMailboxPstCheckpoint checkpoint) {
        string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_options.DestinationPath))!,
            ".mailozaurr-pst-" + checkpoint.StagingId.ToString("N"));
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) {
            throw new IOException($"The PST work directory '{path}' is a link.");
        }
        return path;
    }

    private FileStream? LockCheckpoint() {
        if (CheckpointPath is not { } path) return null;
        string destination = Path.GetFullPath(_options.DestinationPath);
        string partPrefix = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetFileNameWithoutExtension(destination) + ".part");
        if (string.Equals(path, destination, PathComparison) || (_options.Split != null && path.StartsWith(partPrefix, PathComparison) &&
            path.EndsWith(".pst", PathComparison) && path.Length > partPrefix.Length + 4 &&
            int.TryParse(path.Substring(partPrefix.Length, path.Length - partPrefix.Length - 4), NumberStyles.None,
                CultureInfo.InvariantCulture, out int number) && number > 0 && string.Equals(path, PartPath(number), PathComparison))) {
            throw new ArgumentException("The checkpoint path must differ from the PST output paths.");
        }
        return LockPath(path + ".mailozaurr.lock");
    }

    private FileStream? LockDestination() => CheckpointPath == null ? null
        : LockPath(Path.GetFullPath(_options.DestinationPath) + ".mailozaurr-destination.lock");

    private static FileStream LockPath(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Keep the lock path stable across processes. Opening it does not change any existing file.
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
    }

    private bool OpenWriter(string fingerprint, CancellationToken cancellationToken) {
        if (CheckpointPath is { } path && File.Exists(path)) {
            GraphMailboxPstCheckpoint checkpoint = GraphMailboxPstCheckpoint.Read(path);
            if (_options.Resume) {
                _checkpoint = checkpoint;
                GraphMailboxPstExportState start = checkpoint.ReadStart();
                ValidateCheckpointState(start, fingerprint);
                ValidateCompletedParts(start, cancellationToken);
                GraphMailboxPstExportState? finalizing = checkpoint.ReadFinalizing();
                if (finalizing != null && RecoverFinalizingPart(start, finalizing, cancellationToken)) return true;
                RestoreActivePart(start, finalizing != null);
                return true;
            }
            if (!_options.Overwrite) {
                throw new InvalidOperationException(
                    $"A checkpoint from an earlier export exists at '{path}'. Resume it, or allow overwriting to start again.");
            }
            using (JsonDocument previous = JsonDocument.Parse(checkpoint.ReadStart().Fingerprint))
            using (JsonDocument current = JsonDocument.Parse(fingerprint)) {
                if (!string.Equals(previous.RootElement.GetProperty("destination").GetString(),
                        current.RootElement.GetProperty("destination").GetString(), StringComparison.Ordinal)) {
                    throw new InvalidDataException("The checkpoint belongs to a different PST destination.");
                }
            }
            RemoveWorkingDirectory(checkpoint);
        }
        _state = new GraphMailboxPstExportState { Fingerprint = fingerprint };
        StartPart(1);
        return false;
    }

    private bool RecoverFinalizingPart(GraphMailboxPstExportState start, GraphMailboxPstExportState finalizing, CancellationToken cancellationToken) {
        ValidateCheckpointState(finalizing, start.Fingerprint);
        if (finalizing.PartNumber != start.PartNumber || finalizing.CompletedParts.Count < start.PartNumber - 1 ||
            finalizing.CompletedParts.Count > start.PartNumber ||
            !HasCompletedPrefix(finalizing, start)) {
            throw new InvalidDataException("The PST finalization checkpoint disagrees with the part-start checkpoint.");
        }
        string stage = Path.Combine(WorkingDirectory(_checkpoint!), "part.pst");
        if (!File.Exists(stage) && finalizing.CompletedParts.Count < finalizing.PartNumber) return false;

        _state = finalizing;
        PublishStagedPart(cancellationToken);
        if (!_checkpoint!.IsFinalPart) StartPart(_state.PartNumber + 1);
        return true;
    }

    private void RestoreActivePart(GraphMailboxPstExportState start, bool wasFinalizing) {
        string directory = WorkingDirectory(_checkpoint!);
        string nativeCheckpoint = Path.Combine(directory, "writer.checkpoint");
        if (File.Exists(nativeCheckpoint)) {
            EmailStorePstWriter? writer = null;
            byte[]? applicationState = null;
            try {
                writer = EmailStorePstWriter.Resume(nativeCheckpoint, out applicationState);
            } catch (Exception exception) when (wasFinalizing &&
                (exception is InvalidDataException or FileNotFoundException or DirectoryNotFoundException)) {
                // OfficeIMO may have removed its journals immediately before publishing the staged part.
                // The part-start state can replay this unpublished part without changing earlier parts.
            }
            if (writer != null) {
                try {
                    GraphMailboxPstExportState active = GraphMailboxPstExportState.Deserialize(applicationState);
                    ValidateCheckpointState(active, start.Fingerprint);
                    if (active.PartNumber != start.PartNumber || active.CompletedParts.Count != start.CompletedParts.Count ||
                        !HasCompletedPrefix(active, start)) {
                        throw new InvalidDataException("The native PST checkpoint disagrees with the part-start checkpoint.");
                    }
                    _state = active;
                    _writer = writer;
                    return;
                } catch {
                    writer.Dispose();
                    throw;
                }
            }
        }
        _state = start;
        RemoveWorkingDirectory(_checkpoint!);
        CreateWriter();
    }

    private static void ValidateCheckpointState(GraphMailboxPstExportState state, string fingerprint) {
        if (state.PartNumber < 1) throw new InvalidDataException("The checkpoint contains an invalid PST part number.");
        if (!string.Equals(state.Fingerprint, fingerprint, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "The checkpoint belongs to an export with a different mailbox, destination, split settings, item types, item size limit, folder filters or date range.");
        }
    }

    private static bool HasCompletedPrefix(GraphMailboxPstExportState candidate, GraphMailboxPstExportState start) {
        return candidate.CompletedParts.Take(start.CompletedParts.Count).Zip(start.CompletedParts, (current, saved) =>
            current.Number == saved.Number && current.Path == saved.Path && current.Sha256 == saved.Sha256 &&
            current.ItemCount == saved.ItemCount && current.FolderCount == saved.FolderCount && current.Bytes == saved.Bytes &&
            current.EstimatedBytes == saved.EstimatedBytes && current.Verified == saved.Verified &&
            current.VerifiedItemCount == saved.VerifiedItemCount).All(matches => matches);
    }

    private void ValidateCompletedParts(GraphMailboxPstExportState state, CancellationToken cancellationToken) {
        if (state.CompletedParts.Count != state.PartNumber - 1 || (_options.Split == null && state.CompletedParts.Count != 0)) {
            throw new InvalidDataException("The checkpoint contains inconsistent completed PST part numbers.");
        }
        for (int i = 0; i < state.CompletedParts.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCompletedPart(state.CompletedParts[i], i + 1, PartPath(i + 1));
        }
    }

    private void ValidateCompletedPart(GraphMailboxPstExportPart part, int number, string actualPath) {
        string expected = PartPath(number);
        if (part.Number != number || !string.Equals(part.Path, expected, PathComparison)) {
            throw new InvalidDataException("The checkpoint contains an unexpected completed PST part path.");
        }
        if (!File.Exists(actualPath)) throw new InvalidDataException($"Completed PST part '{actualPath}' is missing.");
        if (!string.Equals(ComputeSha256(actualPath), part.Sha256, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException($"Completed PST part '{actualPath}' no longer matches its checkpoint SHA-256.");
        }
    }

    private void StartPart(int number) {
        string destination = PartPath(number);
        if (File.Exists(destination) && !_options.Overwrite) throw new IOException($"The destination PST '{destination}' already exists.");
        if (_checkpoint != null) RemoveWorkingDirectory(_checkpoint);
        _state.PartNumber = number;
        _state.PartItems = 0;
        _state.PartEstimatedBytes = 0;
        _state.PstFolders.Clear();
        if (CheckpointPath is { } path) {
            _checkpoint = new GraphMailboxPstCheckpoint(_state);
            _checkpoint.Save(path);
        }
        CreateWriter();
    }

    private void CreateWriter() {
        string destination = PartPath(_state.PartNumber);
        string? nativeCheckpoint = null;
        if (_checkpoint != null) {
            string directory = WorkingDirectory(_checkpoint);
            Directory.CreateDirectory(directory);
            destination = Path.Combine(directory, "part.pst");
            nativeCheckpoint = Path.Combine(directory, "writer.checkpoint");
        }
        var writerOptions = new EmailStorePstWriterOptions(
            displayName: _options.DisplayName ?? _options.Mailbox,
            overwriteExisting: nativeCheckpoint == null && _options.Overwrite,
            failOnLoss: false,
            checkpointPath: nativeCheckpoint,
            checkpointIntervalItems: int.MaxValue,
            retainCheckpointOnDispose: nativeCheckpoint != null);
        _writer = EmailStorePstWriter.Create(destination, writerOptions);
        _writerFinalizing = false;
        _itemsSinceCheckpoint = 0;
        if (nativeCheckpoint != null) _writer.Checkpoint(_state.Serialize());
    }

    private void CompletePart(bool isFinalPart, CancellationToken cancellationToken) {
        EmailStorePstWriter writer = _writer ?? throw new InvalidOperationException("No PST part is open.");
        if (_checkpoint != null) {
            writer.Checkpoint(_state.Serialize());
            _checkpoint.SaveFinalizing(CheckpointPath!, _state, isFinalPart);
        }
        _writerFinalizing = true;
        EmailStorePstWriteReport report = writer.Complete(cancellationToken);
        _writer = null;
        writer.Dispose();
        _diagnostics.AddRange(report.NativeDiagnostics);
        if (_checkpoint != null) PublishStagedPart(cancellationToken);
        else _state.CompletedParts.Add(DescribePart(report.DestinationPath, report.FolderCount, cancellationToken));
    }

    private void PublishStagedPart(CancellationToken cancellationToken) {
        string stage = Path.Combine(WorkingDirectory(_checkpoint!), "part.pst");
        string destination = PartPath(_state.PartNumber);
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(stage) && (File.GetAttributes(stage) & FileAttributes.ReparsePoint) != 0) {
            throw new IOException($"The staged PST '{stage}' is a link.");
        }
        if (_state.CompletedParts.Count < _state.PartNumber) {
            _state.CompletedParts.Add(DescribePart(stage, _state.PstFolders.Count, cancellationToken));
            _checkpoint!.SaveFinalizing(CheckpointPath!, _state, _checkpoint.IsFinalPart);
        } else {
            bool staged = File.Exists(stage);
            ValidateCompletedPart(_state.CompletedParts[_state.PartNumber - 1], _state.PartNumber, staged ? stage : destination);
            if (!staged) return;
        }
        if (File.Exists(destination) && _options.Overwrite) {
            if (GraphMailboxPstCheckpoint.HasHeader(destination)) {
                throw new IOException("The final PST path resolves to an export checkpoint.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(stage, destination, null);
        } else {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stage, destination);
        }
    }

    private GraphMailboxPstExportPart DescribePart(string path, int folders, CancellationToken cancellationToken) {
        int? verifiedItems = _options.Verify ? CountItems(path, cancellationToken) : null;
        return new GraphMailboxPstExportPart(_state.PartNumber, PartPath(_state.PartNumber), _state.PartItems, folders,
            new FileInfo(path).Length, _state.PartEstimatedBytes, ComputeSha256(path),
            verifiedItems.HasValue ? verifiedItems.Value == _state.PartItems : null, verifiedItems);
    }

    private void FinishCheckpoint() {
        if (_checkpoint == null) return;
        RemoveWorkingDirectory(_checkpoint);
        string path = CheckpointPath!;
        if (GraphMailboxPstCheckpoint.Read(path).StagingId != _checkpoint.StagingId) {
            throw new IOException("The mailbox export checkpoint changed before cleanup.");
        }
        File.Delete(path);
        _checkpoint = null;
    }

    private void RemoveWorkingDirectory(GraphMailboxPstCheckpoint checkpoint) {
        string path = WorkingDirectory(checkpoint);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private void CloseIncompleteWriter() {
        EmailStorePstWriter? writer = _writer;
        if (writer == null) return;
        _writer = null;
        try {
            if (_writerFinalizing) return;
            if (_checkpoint != null) writer.Checkpoint(_state.Serialize());
            else writer.Abandon();
        } catch (Exception) {
            // Recover from the part-start checkpoint if native state cannot be committed consistently.
            try { writer.Abandon(); } catch (InvalidOperationException) { }
        } finally {
            writer.Dispose();
        }
    }
}
