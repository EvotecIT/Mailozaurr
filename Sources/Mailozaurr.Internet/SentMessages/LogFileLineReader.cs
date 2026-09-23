using System.Text;

namespace Mailozaurr;

internal static class LogFileLineReader {
    internal readonly struct LineRecord {
        public LineRecord(long offset, long endOffset, bool isComplete, string line) {
            Offset = offset;
            EndOffset = endOffset;
            IsComplete = isComplete;
            Line = line;
        }

        public long Offset { get; }

        public long EndOffset { get; }

        public bool IsComplete { get; }

        public string Line { get; }
    }

    public static IEnumerable<LineRecord> ReadLinesWithOffsets(string filePath, long startOffset = 0) {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (startOffset > 0) stream.Seek(startOffset, SeekOrigin.Begin);
        foreach (var line in ReadLinesWithOffsets(stream)) {
            yield return line;
        }
    }

    private static IEnumerable<LineRecord> ReadLinesWithOffsets(FileStream stream) {
        var buffer = new List<byte>();

        while (true) {
            var offset = stream.Position;
            var endOfFile = false;

            while (true) {
                var value = stream.ReadByte();
                if (value < 0) {
                    endOfFile = true;
                    break;
                }

                if (value == '\n') {
                    if (buffer.Count > 0 && buffer[buffer.Count - 1] == '\r') {
                        buffer.RemoveAt(buffer.Count - 1);
                    }

                    break;
                }

                buffer.Add((byte)value);
            }

            if (endOfFile && buffer.Count == 0) {
                yield break;
            }

            yield return new LineRecord(offset, stream.Position, !endOfFile,
                buffer.Count == 0 ? string.Empty : Encoding.UTF8.GetString(buffer.ToArray()));
            buffer.Clear();

            if (endOfFile) {
                yield break;
            }
        }
    }
}
