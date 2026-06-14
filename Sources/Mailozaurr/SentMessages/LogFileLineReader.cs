using System.Text;

namespace Mailozaurr;

internal static class LogFileLineReader {
    internal readonly struct LineRecord {
        public LineRecord(long offset, string line) {
            Offset = offset;
            Line = line;
        }

        public long Offset { get; }

        public string Line { get; }
    }

    public static IEnumerable<LineRecord> ReadLinesWithOffsets(string filePath) {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

            yield return new LineRecord(offset, buffer.Count == 0 ? string.Empty : Encoding.UTF8.GetString(buffer.ToArray()));
            buffer.Clear();

            if (endOfFile) {
                yield break;
            }
        }
    }
}