using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OfficeIMO.Email;
using OfficeIMO.Email.Store;

namespace Mailozaurr.MailboxExport;

/// <summary>
/// Streams a mailbox into one or more Unicode PST files through the OfficeIMO.Email PST writer.
/// </summary>
/// <remarks>
/// Messages are downloaded one at a time as MIME and written straight into the PST; nothing is staged on disk
/// apart from the writer's own working files. With split options, a new numbered part starts before an item
/// would push the current part over its size estimate. With a checkpoint path, progress is committed atomically
/// with the writer checkpoint so an interrupted export resumes without duplicates.
/// </remarks>
public sealed class GraphMailboxPstExporter {
    private const string MailPrefix = "m:";
    private const string ContactPrefix = "c:";
    private const string CalendarPrefix = "e:";
    private const string TaskPrefix = "t:";

    private readonly IGraphMailboxExportSource _source;
    private readonly GraphMailboxPstExportOptions _options;
    private readonly List<EmailStoreDiagnostic> _diagnostics = new List<EmailStoreDiagnostic>();
    private readonly Dictionary<string, PlannedFolder> _folders = new Dictionary<string, PlannedFolder>(StringComparer.Ordinal);
    private readonly List<PlannedFolder> _folderOrder = new List<PlannedFolder>();
    private GraphMailboxPstExportState _state = new GraphMailboxPstExportState();
    private EmailStorePstWriter? _writer;
    private int _itemsSinceCheckpoint;
    private long? _expectedItems;

    /// <summary>Creates an exporter.</summary>
    public GraphMailboxPstExporter(IGraphMailboxExportSource source, GraphMailboxPstExportOptions options) {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.DestinationPath)) {
            throw new ArgumentException("A destination path is required.", nameof(options));
        }
        if (options.ItemTypes == GraphMailboxExportItemTypes.None) {
            throw new ArgumentException("At least one item type must be selected.", nameof(options));
        }
        if (options.MaxItemBytes <= 0 || options.MaxItemBytes > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxItemBytes must be between 1 byte and 2 GiB.");
        }
        if (options.CheckpointIntervalItems <= 0) {
            throw new ArgumentOutOfRangeException(nameof(options), "CheckpointIntervalItems must be positive.");
        }
        if (options.StartDate.HasValue && options.EndDate.HasValue && options.StartDate >= options.EndDate) {
            throw new ArgumentException("StartDate must be earlier than EndDate.", nameof(options));
        }
    }

    /// <summary>Builds the path of a numbered part, using the same <c>name.partNNN.pst</c> scheme as OfficeIMO PST splitting.</summary>
    public static string GetPartPath(string destinationPath, int number) {
        string full = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(full) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(full);
        return Path.Combine(directory, string.Concat(stem, ".part", number.ToString("D3", CultureInfo.InvariantCulture), ".pst"));
    }

    /// <summary>Runs the export.</summary>
    public async Task<GraphMailboxPstExportReport> ExportAsync(CancellationToken cancellationToken = default) {
        var stopwatch = Stopwatch.StartNew();
        string fingerprint = GraphMailboxPstExportState.BuildFingerprint(_options);
        bool resumed = OpenWriter(fingerprint, cancellationToken);
        bool completed = false;
        try {
            await PlanFoldersAsync(cancellationToken).ConfigureAwait(false);
            foreach (PlannedFolder folder in _folderOrder) {
                if (!folder.Selected || _state.DoneFolders.Contains(folder.Key)) continue;
                cancellationToken.ThrowIfCancellationRequested();
                EnsurePstFolder(folder);
                ReportProgress(folder.Path);
                switch (folder.ItemType) {
                    case GraphMailboxExportItemTypes.Mail:
                        await ExportMessagesAsync(folder, cancellationToken).ConfigureAwait(false);
                        break;
                    case GraphMailboxExportItemTypes.Contacts:
                        await ExportResourcesAsync(folder, _source.GetContactsAsync, cancellationToken).ConfigureAwait(false);
                        break;
                    case GraphMailboxExportItemTypes.Calendar:
                        await ExportResourcesAsync(folder, _source.GetEventsAsync, cancellationToken).ConfigureAwait(false);
                        break;
                    case GraphMailboxExportItemTypes.Tasks:
                        await ExportResourcesAsync(folder, _source.GetTasksAsync, cancellationToken).ConfigureAwait(false);
                        break;
                }
                _state.DoneFolders.Add(folder.Key);
                _state.ResetCursor(null);
            }
            CompletePart(cancellationToken);
            completed = true;
        } finally {
            if (!completed) CloseIncompleteWriter();
        }
        return new GraphMailboxPstExportReport(
            _options.Mailbox,
            _state.CompletedParts.ToArray(),
            new GraphMailboxExportCounts(_state.Messages, _state.Contacts, _state.Events, _state.Tasks),
            _state.Skipped,
            _state.Failed,
            _state.Issues.ToArray(),
            _diagnostics.ToArray(),
            resumed,
            stopwatch.Elapsed);
    }

    private bool OpenWriter(string fingerprint, CancellationToken cancellationToken) {
        string? checkpoint = _options.CheckpointPath;
        if (checkpoint != null && File.Exists(checkpoint)) {
            if (_options.Resume) {
                _writer = EmailStorePstWriter.Resume(checkpoint, out byte[]? applicationState);
                try {
                    GraphMailboxPstExportState state = GraphMailboxPstExportState.Deserialize(applicationState);
                    if (!string.Equals(state.Fingerprint, fingerprint, StringComparison.Ordinal)) {
                        throw new InvalidOperationException(
                            "The checkpoint belongs to an export with a different mailbox, destination, split settings, item types, item size limit, folder filters or date range.");
                    }
                    ValidateCompletedParts(state, cancellationToken);
                    _state = state;
                } catch {
                    _writer.Dispose();
                    _writer = null;
                    throw;
                }
                return true;
            }
            if (!_options.Overwrite) {
                throw new InvalidOperationException(
                    $"A checkpoint from an earlier export exists at '{checkpoint}'. Resume it, or allow overwriting to start again.");
            }
            EmailStorePstWriter.DeleteCheckpoint(checkpoint);
        }
        _state = new GraphMailboxPstExportState { Fingerprint = fingerprint };
        StartPart(1);
        return false;
    }

    private void ValidateCompletedParts(GraphMailboxPstExportState state, CancellationToken cancellationToken) {
        if (state.PartNumber < 1 || state.CompletedParts.Count != state.PartNumber - 1 ||
            (_options.Split == null && state.CompletedParts.Count != 0)) {
            throw new InvalidDataException("The checkpoint contains inconsistent completed PST part numbers.");
        }

        StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (int i = 0; i < state.CompletedParts.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
            GraphMailboxPstExportPart part = state.CompletedParts[i];
            string expectedPath = GetPartPath(_options.DestinationPath, i + 1);
            if (part.Number != i + 1 || !string.Equals(part.Path, expectedPath, pathComparison)) {
                throw new InvalidDataException("The checkpoint contains an unexpected completed PST part path.");
            }
            if (!File.Exists(expectedPath)) {
                throw new InvalidDataException($"Completed PST part '{expectedPath}' is missing.");
            }
            if (!string.Equals(ComputeSha256(expectedPath), part.Sha256, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException($"Completed PST part '{expectedPath}' no longer matches its checkpoint SHA-256.");
            }
        }
    }

    private void StartPart(int number) {
        _state.PartNumber = number;
        _state.PartItems = 0;
        _state.PartEstimatedBytes = 0;
        _state.PstFolders.Clear();
        string destination = _options.Split == null ? Path.GetFullPath(_options.DestinationPath) : GetPartPath(_options.DestinationPath, number);
        var writerOptions = new EmailStorePstWriterOptions(
            displayName: _options.DisplayName ?? _options.Mailbox,
            overwriteExisting: _options.Overwrite,
            failOnLoss: false,
            checkpointPath: _options.CheckpointPath,
            // Checkpoints are taken explicitly so the exporter state always matches the writer state.
            checkpointIntervalItems: int.MaxValue,
            retainCheckpointOnDispose: _options.CheckpointPath != null);
        _writer = EmailStorePstWriter.Create(destination, writerOptions);
        _itemsSinceCheckpoint = 0;
        if (_options.CheckpointPath != null) _writer.Checkpoint(_state.Serialize());
    }

    private void CompletePart(CancellationToken cancellationToken) {
        EmailStorePstWriter writer = _writer ?? throw new InvalidOperationException("No PST part is open.");
        EmailStorePstWriteReport report = writer.Complete(cancellationToken);
        writer.Dispose();
        _writer = null;
        _diagnostics.AddRange(report.NativeDiagnostics);
        string path = report.DestinationPath;
        bool? verified = null;
        int? verifiedItems = null;
        if (_options.Verify) {
            verifiedItems = CountItems(path, cancellationToken);
            verified = verifiedItems == _state.PartItems;
        }
        _state.CompletedParts.Add(new GraphMailboxPstExportPart(
            _state.PartNumber,
            path,
            _state.PartItems,
            report.FolderCount,
            report.BytesWritten,
            _state.PartEstimatedBytes,
            ComputeSha256(path),
            verified,
            verifiedItems));
    }

    private void CloseIncompleteWriter() {
        EmailStorePstWriter? writer = _writer;
        if (writer == null) return;
        _writer = null;
        try {
            if (_options.CheckpointPath != null) {
                writer.Checkpoint(_state.Serialize());
            } else {
                writer.Abandon();
            }
        } catch (Exception) {
            // A checkpoint that cannot be committed must not be replaced by one that disagrees with the exporter state.
            try { writer.Abandon(); } catch (InvalidOperationException) { }
        } finally {
            writer.Dispose();
        }
    }

    private async Task PlanFoldersAsync(CancellationToken cancellationToken) {
        GraphMailboxExportItemTypes types = _options.ItemTypes;
        long expected = 0;
        bool expectedKnown = true;
        if ((types & GraphMailboxExportItemTypes.Mail) != 0) {
            foreach (GraphExportFolder folder in await _source.GetMailFoldersAsync(cancellationToken).ConfigureAwait(false)) {
                PlannedFolder planned = AddPlannedFolder(MailPrefix, folder, GraphMailboxExportItemTypes.Mail, "IPF.Note");
                if (!planned.Selected) continue;
                if (folder.TotalItemCount.HasValue) expected += folder.TotalItemCount.Value;
                else expectedKnown = false;
            }
        }
        if ((types & GraphMailboxExportItemTypes.Contacts) != 0) {
            foreach (GraphExportFolder folder in await _source.GetContactFoldersAsync(cancellationToken).ConfigureAwait(false)) {
                AddPlannedFolder(ContactPrefix, folder, GraphMailboxExportItemTypes.Contacts, "IPF.Contact");
            }
            expectedKnown = false;
        }
        if ((types & GraphMailboxExportItemTypes.Calendar) != 0) {
            foreach (GraphExportFolder folder in await _source.GetCalendarsAsync(cancellationToken).ConfigureAwait(false)) {
                AddPlannedFolder(CalendarPrefix, folder, GraphMailboxExportItemTypes.Calendar, "IPF.Appointment");
            }
            expectedKnown = false;
        }
        if ((types & GraphMailboxExportItemTypes.Tasks) != 0) {
            foreach (GraphExportFolder folder in await _source.GetTaskListsAsync(cancellationToken).ConfigureAwait(false)) {
                AddPlannedFolder(TaskPrefix, folder, GraphMailboxExportItemTypes.Tasks, "IPF.Task");
            }
            expectedKnown = false;
        }
        _expectedItems = expectedKnown ? expected : null;
    }

    private PlannedFolder AddPlannedFolder(string prefix, GraphExportFolder folder,
        GraphMailboxExportItemTypes itemType, string containerClass) {
        string key = prefix + folder.Id;
        PlannedFolder? parent = null;
        if (folder.ParentId != null) _folders.TryGetValue(prefix + folder.ParentId, out parent);
        string path = parent == null ? folder.DisplayName : parent.Path + "\\" + folder.DisplayName;
        bool included = _options.IncludeFolders.Count == 0 || MatchesSelfOrParent(path, _options.IncludeFolders);
        bool excluded = MatchesSelfOrParent(path, _options.ExcludeFolders);
        var planned = new PlannedFolder(key, folder, parent, path, itemType, containerClass, included && !excluded);
        _folders[key] = planned;
        _folderOrder.Add(planned);
        return planned;
    }

    private string EnsurePstFolder(PlannedFolder folder) {
        if (_state.PstFolders.TryGetValue(folder.Key, out string? existing)) return existing;
        string? parentId = folder.Parent == null ? null : EnsurePstFolder(folder.Parent);
        EmailStorePstWriter writer = _writer ?? throw new InvalidOperationException("No PST part is open.");
        EmailStoreSpecialFolderKind kind = folder.Parent == null ? ToSpecialFolderKind(folder.Source.Role) : EmailStoreSpecialFolderKind.Unknown;
        string id = kind != EmailStoreSpecialFolderKind.Unknown && EmailStorePstWriter.CanAssignSpecialFolderKind(kind)
            ? writer.AddFolder(folder.Source.DisplayName, kind, parentId, folder.ContainerClass)
            : writer.AddFolder(folder.Source.DisplayName, parentId, folder.ContainerClass);
        _state.PstFolders[folder.Key] = id;
        return id;
    }

    private static EmailStoreSpecialFolderKind ToSpecialFolderKind(GraphExportFolderRole role) => role switch {
        GraphExportFolderRole.Inbox => EmailStoreSpecialFolderKind.Inbox,
        GraphExportFolderRole.SentItems => EmailStoreSpecialFolderKind.SentItems,
        GraphExportFolderRole.Drafts => EmailStoreSpecialFolderKind.Drafts,
        GraphExportFolderRole.JunkEmail => EmailStoreSpecialFolderKind.JunkEmail,
        GraphExportFolderRole.Outbox => EmailStoreSpecialFolderKind.Outbox,
        GraphExportFolderRole.Contacts => EmailStoreSpecialFolderKind.Contacts,
        GraphExportFolderRole.Calendar => EmailStoreSpecialFolderKind.Calendar,
        GraphExportFolderRole.Tasks => EmailStoreSpecialFolderKind.Tasks,
        // The writer owns Deleted Items, so mailbox Deleted Items is written as an ordinary folder.
        _ => EmailStoreSpecialFolderKind.Unknown
    };

    private async Task ExportMessagesAsync(PlannedFolder folder, CancellationToken cancellationToken) {
        bool resuming = string.Equals(_state.CursorFolder, folder.Key, StringComparison.Ordinal);
        if (!resuming) _state.ResetCursor(folder.Key);
        DateTimeOffset? from = _options.StartDate;
        if (resuming && _state.CursorReceived.HasValue && (!from.HasValue || _state.CursorReceived > from)) {
            from = _state.CursorReceived;
        }
        string? link = null;
        do {
            GraphExportPage<GraphExportMessageInfo> page = await _source.GetMessagesAsync(
                folder.Source.Id, from, _options.EndDate, link, cancellationToken).ConfigureAwait(false);
            foreach (GraphExportMessageInfo message in page.Items) {
                cancellationToken.ThrowIfCancellationRequested();
                if (_state.CursorIds.Contains(message.Id)) continue;
                await ExportMessageAsync(folder, message, cancellationToken).ConfigureAwait(false);
                if (message.ReceivedDateTime.HasValue && message.ReceivedDateTime != _state.CursorReceived) {
                    _state.CursorReceived = message.ReceivedDateTime;
                    _state.CursorIds.Clear();
                }
                _state.CursorIds.Add(message.Id);
                AfterItem(folder.Path);
            }
            link = page.NextLink;
        } while (link != null);
    }

    private async Task ExportMessageAsync(PlannedFolder folder, GraphExportMessageInfo message, CancellationToken cancellationToken) {
        byte[] mime;
        try {
            mime = await _source.GetMessageMimeAsync(message.Id, _options.MaxItemBytes, cancellationToken).ConfigureAwait(false);
        } catch (InvalidDataException exception) {
            RecordIssue(GraphMailboxExportIssueKind.Skipped, GraphMailboxExportItemTypes.Mail, folder.Path,
                message.Id, message.Subject, exception.Message);
            return;
        } catch (Exception exception) when (!IsCancellation(exception, cancellationToken)) {
            HandleFailure(GraphMailboxExportItemTypes.Mail, folder.Path, message.Id, message.Subject, exception);
            return;
        }
        EmailDocument document;
        try {
            document = EmailDocument.Load(mime, cancellationToken: cancellationToken);
            ApplyListingMetadata(document, message);
        } catch (Exception exception) when (!IsCancellation(exception, cancellationToken)) {
            HandleFailure(GraphMailboxExportItemTypes.Mail, folder.Path, message.Id, message.Subject, exception);
            return;
        }
        if (WriteItem(folder, document, mime.LongLength, message.Id, message.Subject, cancellationToken)) {
            _state.Messages++;
        }
    }

    private async Task ExportResourcesAsync(PlannedFolder folder,
        Func<string, string?, CancellationToken, Task<GraphExportPage<JsonElement>>> getPage,
        CancellationToken cancellationToken) {
        bool resuming = string.Equals(_state.CursorFolder, folder.Key, StringComparison.Ordinal);
        if (!resuming) _state.ResetCursor(folder.Key);
        string? link = null;
        do {
            GraphExportPage<JsonElement> page = await getPage(folder.Source.Id, link, cancellationToken).ConfigureAwait(false);
            foreach (JsonElement resource in page.Items) {
                cancellationToken.ThrowIfCancellationRequested();
                string id = GraphMailboxExportSource.GetString(resource, "id") ?? string.Empty;
                if (id.Length > 0 && _state.CursorIds.Contains(id)) continue;
                ExportResource(folder, resource, id, cancellationToken);
                if (id.Length > 0) _state.CursorIds.Add(id);
                AfterItem(folder.Path);
            }
            link = page.NextLink;
        } while (link != null);
    }

    private void ExportResource(PlannedFolder folder, JsonElement resource, string id, CancellationToken cancellationToken) {
        GraphMailboxExportItemTypes type = folder.ItemType;
        string? subject = GraphMailboxExportSource.GetString(resource, type == GraphMailboxExportItemTypes.Tasks ? "title" :
            type == GraphMailboxExportItemTypes.Contacts ? "displayName" : "subject");
        EmailDocument document;
        string? warning = null;
        try {
            if (type == GraphMailboxExportItemTypes.Calendar && !EventInDateRange(resource)) return;
            document = type switch {
                GraphMailboxExportItemTypes.Contacts => GraphMailboxItemMapper.MapContact(resource),
                GraphMailboxExportItemTypes.Calendar => GraphMailboxItemMapper.MapEvent(resource, out warning),
                _ => GraphMailboxItemMapper.MapTask(resource, out warning)
            };
        } catch (Exception exception) when (!IsCancellation(exception, cancellationToken)) {
            HandleFailure(type, folder.Path, id, subject, exception);
            return;
        }
        long estimate = Encoding.UTF8.GetByteCount(resource.GetRawText());
        if (!WriteItem(folder, document, estimate, id, subject, cancellationToken)) return;
        if (warning != null) RecordIssue(GraphMailboxExportIssueKind.Partial, type, folder.Path, id, subject, warning);
        switch (type) {
            case GraphMailboxExportItemTypes.Contacts: _state.Contacts++; break;
            case GraphMailboxExportItemTypes.Calendar: _state.Events++; break;
            default: _state.Tasks++; break;
        }
    }

    private bool EventInDateRange(JsonElement graphEvent) {
        if (!_options.StartDate.HasValue && !_options.EndDate.HasValue) return true;
        DateTimeOffset? start = GraphMailboxItemMapper.GetEventStart(graphEvent);
        if (_options.EndDate.HasValue && start.HasValue && start.Value >= _options.EndDate.Value) return false;
        return !_options.StartDate.HasValue || !GraphMailboxItemMapper.EventEndsBefore(graphEvent, _options.StartDate.Value);
    }

    private bool WriteItem(PlannedFolder folder, EmailDocument document, long payloadBytes, string id, string? subject,
        CancellationToken cancellationToken) {
        long estimate = payloadBytes + (_options.Split?.PerItemOverheadBytes ?? 0);
        EmailStorePstSplitOptions? split = _options.Split;
        if (split != null && _state.PartItems > 0 && _state.PartEstimatedBytes + estimate > split.MaxEstimatedBytesPerPart) {
            if (_state.PartNumber >= split.MaxParts) {
                throw new InvalidOperationException(
                    $"The export needs more than {split.MaxParts} parts at the selected split size. Increase the part limit to continue from a valid checkpoint.");
            }
            int next = _state.PartNumber + 1;
            CompletePart(cancellationToken);
            StartPart(next);
        }
        string pstFolderId = EnsurePstFolder(folder);
        try {
            _writer!.AddItem(pstFolderId, document, cancellationToken: cancellationToken);
        } catch (Exception exception) when (!IsCancellation(exception, cancellationToken) &&
            exception is not IOException && exception is not UnauthorizedAccessException) {
            HandleFailure(folder.ItemType, folder.Path, id, subject, exception);
            return false;
        }
        _state.PartItems++;
        _state.PartEstimatedBytes += estimate;
        return true;
    }

    private void AfterItem(string folderPath) {
        _itemsSinceCheckpoint++;
        if (_options.CheckpointPath != null && _itemsSinceCheckpoint >= _options.CheckpointIntervalItems) {
            _writer!.Checkpoint(_state.Serialize());
            _itemsSinceCheckpoint = 0;
        }
        ReportProgress(folderPath);
    }

    private void ReportProgress(string folderPath) {
        _options.Progress?.Report(new GraphMailboxPstExportProgress(
            folderPath, _state.Written, _state.Skipped, _state.Failed, _expectedItems,
            _state.PartNumber, _state.PartEstimatedBytes));
    }

    private void HandleFailure(GraphMailboxExportItemTypes type, string folderPath, string id, string? subject, Exception exception) {
        if (_options.StopOnItemError) {
            throw new InvalidOperationException($"Exporting item '{id}' from '{folderPath}' failed: {exception.Message}", exception);
        }
        RecordIssue(GraphMailboxExportIssueKind.Failed, type, folderPath, id, subject, exception.Message);
    }

    private void RecordIssue(GraphMailboxExportIssueKind kind, GraphMailboxExportItemTypes type, string folderPath,
        string id, string? subject, string message) {
        if (kind == GraphMailboxExportIssueKind.Failed) _state.Failed++;
        else if (kind == GraphMailboxExportIssueKind.Skipped) _state.Skipped++;
        if (_state.Issues.Count < _options.MaxReportedFailures) {
            _state.Issues.Add(new GraphMailboxExportIssue(kind, type, folderPath, id, subject, message));
        }
    }

    private static void ApplyListingMetadata(EmailDocument document, GraphExportMessageInfo message) {
        if (message.IsRead.HasValue) document.MessageMetadata.IsRead = message.IsRead;
        if (message.IsDraft == true) document.MessageMetadata.IsDraft = true;
        if (message.ReceivedDateTime.HasValue) document.ReceivedDate = message.ReceivedDateTime;
        foreach (string category in message.Categories) document.MessageMetadata.Categories.AddIfMissing(category);
        EmailMessageImportance? importance = message.Importance switch {
            "low" => EmailMessageImportance.Low,
            "high" => EmailMessageImportance.High,
            "normal" => EmailMessageImportance.Normal,
            _ => null
        };
        if (importance.HasValue) document.MessageMetadata.Importance = importance;
    }

    private static int CountItems(string path, CancellationToken cancellationToken) {
        using EmailStoreSession session = EmailStoreSession.Open(path, cancellationToken: cancellationToken);
        int count = 0;
        foreach (EmailStoreItemReference _ in session.EnumerateItems(cancellationToken: cancellationToken)) count++;
        return count;
    }

    private static string ComputeSha256(string path) {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash) builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static bool IsCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static bool MatchesSelfOrParent(string path, IList<string> patterns) {
        if (patterns.Count == 0) return false;
        string current = path;
        while (true) {
            foreach (string pattern in patterns) {
                if (WildcardMatch(current, pattern)) return true;
            }
            int separator = current.LastIndexOf('\\');
            if (separator < 0) return false;
            current = current.Substring(0, separator);
        }
    }

    internal static bool WildcardMatch(string value, string pattern) {
        string normalized = pattern.Replace('/', '\\').Trim('\\');
        string regex = "^" + Regex.Escape(normalized).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private sealed class PlannedFolder {
        internal PlannedFolder(string key, GraphExportFolder source, PlannedFolder? parent, string path,
            GraphMailboxExportItemTypes itemType, string containerClass, bool selected) {
            Key = key;
            Source = source;
            Parent = parent;
            Path = path;
            ItemType = itemType;
            ContainerClass = containerClass;
            Selected = selected;
        }

        internal string Key { get; }
        internal GraphExportFolder Source { get; }
        internal PlannedFolder? Parent { get; }
        internal string Path { get; }
        internal GraphMailboxExportItemTypes ItemType { get; }
        internal string ContainerClass { get; }
        internal bool Selected { get; }
    }
}
