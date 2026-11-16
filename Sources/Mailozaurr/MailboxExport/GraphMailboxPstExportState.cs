using System.Text;
using System.Text.Json;

namespace Mailozaurr.MailboxExport;

/// <summary>Exporter progress committed atomically with each PST writer checkpoint.</summary>
internal sealed class GraphMailboxPstExportState {
    internal const int Version = 1;

    internal string Fingerprint { get; set; } = string.Empty;
    internal int PartNumber { get; set; } = 1;
    internal int PartItems { get; set; }
    internal long PartEstimatedBytes { get; set; }
    internal int Messages { get; set; }
    internal int Contacts { get; set; }
    internal int Events { get; set; }
    internal int Tasks { get; set; }
    internal int Skipped { get; set; }
    internal int Failed { get; set; }
    internal List<GraphMailboxPstExportPart> CompletedParts { get; } = new List<GraphMailboxPstExportPart>();
    internal Dictionary<string, string> PstFolders { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
    internal HashSet<string> DoneFolders { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal string? CursorFolder { get; set; }
    internal DateTimeOffset? CursorReceived { get; set; }
    internal HashSet<string> CursorIds { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal List<GraphMailboxExportIssue> Issues { get; } = new List<GraphMailboxExportIssue>();

    internal int Written => Messages + Contacts + Events + Tasks;

    internal void ResetCursor(string? folderKey) {
        CursorFolder = folderKey;
        CursorReceived = null;
        CursorIds.Clear();
    }

    internal byte[] Serialize() {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteString("fingerprint", Fingerprint);
            writer.WriteNumber("partNumber", PartNumber);
            writer.WriteNumber("partItems", PartItems);
            writer.WriteNumber("partEstimatedBytes", PartEstimatedBytes);
            writer.WriteNumber("messages", Messages);
            writer.WriteNumber("contacts", Contacts);
            writer.WriteNumber("events", Events);
            writer.WriteNumber("tasks", Tasks);
            writer.WriteNumber("skipped", Skipped);
            writer.WriteNumber("failed", Failed);
            writer.WriteStartArray("parts");
            foreach (GraphMailboxPstExportPart part in CompletedParts) {
                writer.WriteStartObject();
                writer.WriteNumber("number", part.Number);
                writer.WriteString("path", part.Path);
                writer.WriteNumber("items", part.ItemCount);
                writer.WriteNumber("folders", part.FolderCount);
                writer.WriteNumber("bytes", part.Bytes);
                writer.WriteNumber("estimatedBytes", part.EstimatedBytes);
                writer.WriteString("sha256", part.Sha256);
                if (part.Verified.HasValue) writer.WriteBoolean("verified", part.Verified.Value);
                if (part.VerifiedItemCount.HasValue) writer.WriteNumber("verifiedItems", part.VerifiedItemCount.Value);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartObject("pstFolders");
            foreach (KeyValuePair<string, string> folder in PstFolders) writer.WriteString(folder.Key, folder.Value);
            writer.WriteEndObject();
            WriteStrings(writer, "doneFolders", DoneFolders);
            if (CursorFolder != null) writer.WriteString("cursorFolder", CursorFolder);
            if (CursorReceived.HasValue) writer.WriteString("cursorReceived", CursorReceived.Value);
            WriteStrings(writer, "cursorIds", CursorIds);
            writer.WriteStartArray("issues");
            foreach (GraphMailboxExportIssue issue in Issues) {
                writer.WriteStartObject();
                writer.WriteNumber("kind", (int)issue.Kind);
                writer.WriteNumber("type", (int)issue.ItemType);
                writer.WriteString("folder", issue.FolderPath);
                writer.WriteString("id", issue.ItemId);
                if (issue.Subject != null) writer.WriteString("subject", issue.Subject);
                writer.WriteString("message", issue.Message);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static GraphMailboxPstExportState Deserialize(byte[]? content) {
        if (content == null || content.Length == 0) {
            throw new InvalidDataException("The checkpoint does not contain mailbox export state.");
        }
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("version", out JsonElement version) || version.GetInt32() != Version) {
            throw new InvalidDataException("The checkpoint was written by an unsupported version of the mailbox exporter.");
        }
        var state = new GraphMailboxPstExportState {
            Fingerprint = root.GetProperty("fingerprint").GetString() ?? string.Empty,
            PartNumber = root.GetProperty("partNumber").GetInt32(),
            PartItems = root.GetProperty("partItems").GetInt32(),
            PartEstimatedBytes = root.GetProperty("partEstimatedBytes").GetInt64(),
            Messages = root.GetProperty("messages").GetInt32(),
            Contacts = root.GetProperty("contacts").GetInt32(),
            Events = root.GetProperty("events").GetInt32(),
            Tasks = root.GetProperty("tasks").GetInt32(),
            Skipped = root.GetProperty("skipped").GetInt32(),
            Failed = root.GetProperty("failed").GetInt32()
        };
        foreach (JsonElement part in root.GetProperty("parts").EnumerateArray()) {
            state.CompletedParts.Add(new GraphMailboxPstExportPart(
                part.GetProperty("number").GetInt32(),
                part.GetProperty("path").GetString() ?? string.Empty,
                part.GetProperty("items").GetInt32(),
                part.GetProperty("folders").GetInt32(),
                part.GetProperty("bytes").GetInt64(),
                part.GetProperty("estimatedBytes").GetInt64(),
                part.GetProperty("sha256").GetString() ?? string.Empty,
                part.TryGetProperty("verified", out JsonElement verified) ? verified.GetBoolean() : null,
                part.TryGetProperty("verifiedItems", out JsonElement verifiedItems) ? verifiedItems.GetInt32() : null));
        }
        foreach (JsonProperty folder in root.GetProperty("pstFolders").EnumerateObject()) {
            state.PstFolders[folder.Name] = folder.Value.GetString() ?? string.Empty;
        }
        ReadStrings(root, "doneFolders", state.DoneFolders);
        if (root.TryGetProperty("cursorFolder", out JsonElement cursorFolder)) state.CursorFolder = cursorFolder.GetString();
        if (root.TryGetProperty("cursorReceived", out JsonElement cursorReceived)) state.CursorReceived = cursorReceived.GetDateTimeOffset();
        ReadStrings(root, "cursorIds", state.CursorIds);
        foreach (JsonElement issue in root.GetProperty("issues").EnumerateArray()) {
            state.Issues.Add(new GraphMailboxExportIssue(
                (GraphMailboxExportIssueKind)issue.GetProperty("kind").GetInt32(),
                (GraphMailboxExportItemTypes)issue.GetProperty("type").GetInt32(),
                issue.GetProperty("folder").GetString() ?? string.Empty,
                issue.GetProperty("id").GetString() ?? string.Empty,
                issue.TryGetProperty("subject", out JsonElement subject) ? subject.GetString() : null,
                issue.GetProperty("message").GetString() ?? string.Empty));
        }
        return state;
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values) {
        writer.WriteStartArray(name);
        foreach (string value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void ReadStrings(JsonElement root, string name, ISet<string> target) {
        if (!root.TryGetProperty(name, out JsonElement values)) return;
        foreach (JsonElement value in values.EnumerateArray()) {
            string? text = value.GetString();
            if (text != null) target.Add(text);
        }
    }

    internal static string BuildFingerprint(GraphMailboxPstExportOptions options) {
        var builder = new StringBuilder();
        builder.Append(options.Mailbox.ToUpperInvariant()).Append('|')
            .Append(Path.GetFullPath(options.DestinationPath).ToUpperInvariant()).Append('|')
            .Append(options.Split?.MaxEstimatedBytesPerPart.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append('|')
            .Append((int)options.ItemTypes).Append('|')
            .Append(string.Join(";", options.IncludeFolders)).Append('|')
            .Append(string.Join(";", options.ExcludeFolders)).Append('|')
            .Append(options.StartDate?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append('|')
            .Append(options.EndDate?.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append('|')
            .Append(options.MaxItemBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
