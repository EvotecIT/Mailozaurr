using System.Text;
using System.Text.Json;

namespace Mailozaurr.MailboxExport;

/// <summary>Durable mailbox progress across publication of a staged PST and creation of the next writer.</summary>
internal sealed class GraphMailboxPstCheckpoint {
    private const int Version = 1;
    private const string Format = "Mailozaurr mailbox PST";
    private const int MaxCheckpointBytes = 48 * 1024 * 1024;
    private static readonly byte[] Header = Encoding.UTF8.GetBytes("{\"format\":\"" + Format + "\",");
    private readonly byte[] _startState;
    private byte[]? _finalizingState;

    internal Guid StagingId { get; }
    internal bool IsFinalPart { get; private set; }
    internal GraphMailboxPstExportState ReadStart() => GraphMailboxPstExportState.Deserialize(_startState);
    internal GraphMailboxPstExportState? ReadFinalizing() => _finalizingState == null
        ? null : GraphMailboxPstExportState.Deserialize(_finalizingState);

    internal GraphMailboxPstCheckpoint(GraphMailboxPstExportState startState)
        : this(Guid.NewGuid(), startState.Serialize(), null, false) { }

    private GraphMailboxPstCheckpoint(Guid stagingId, byte[] startState, byte[]? finalizingState, bool isFinalPart) {
        StagingId = stagingId;
        _startState = startState;
        _finalizingState = finalizingState;
        IsFinalPart = isFinalPart;
    }

    internal static GraphMailboxPstCheckpoint Read(string path) {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaxCheckpointBytes || !ReadHeader(stream)) {
            throw new InvalidDataException("The file is not a supported Mailozaurr PST checkpoint.");
        }
        stream.Position = 0;
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("format", out JsonElement format) ||
            format.GetString() != Format || root.GetProperty("version").GetInt32() != Version ||
            !root.GetProperty("stagingId").TryGetGuid(out Guid stagingId) || stagingId == Guid.Empty) {
            throw new InvalidDataException("The file is not a supported Mailozaurr PST checkpoint.");
        }
        bool finalizing = root.TryGetProperty("finalizing", out JsonElement state);
        return new GraphMailboxPstCheckpoint(stagingId, root.GetProperty("start").GetBytesFromBase64(),
            finalizing ? state.GetBytesFromBase64() : null, finalizing && root.GetProperty("lastPart").GetBoolean());
    }

    internal static bool HasHeader(string path) {
        using var stream = File.OpenRead(path);
        return ReadHeader(stream);
    }

    private static bool ReadHeader(Stream stream) {
        foreach (byte value in Header) {
            if (stream.ReadByte() != value) return false;
        }
        return true;
    }

    internal void SaveFinalizing(string path, GraphMailboxPstExportState state, bool isFinalPart) {
        _finalizingState = state.Serialize();
        IsFinalPart = isFinalPart;
        Save(path);
    }

    internal void Save(string path) {
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, Path.GetRandomFileName());
        try {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                using (var writer = new Utf8JsonWriter(stream)) {
                    writer.WriteStartObject();
                    writer.WriteString("format", Format);
                    writer.WriteNumber("version", Version);
                    writer.WriteString("stagingId", StagingId);
                    writer.WriteBase64String("start", _startState);
                    if (_finalizingState != null) {
                        writer.WriteBase64String("finalizing", _finalizingState);
                        writer.WriteBoolean("lastPart", IsFinalPart);
                    }
                    writer.WriteEndObject();
                }
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        } finally {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
