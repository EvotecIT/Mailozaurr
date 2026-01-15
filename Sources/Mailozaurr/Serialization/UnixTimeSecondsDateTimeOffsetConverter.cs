using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mailozaurr;

internal sealed class UnixTimeSecondsDateTimeOffsetConverter : JsonConverter<DateTimeOffset> {
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) {
            return DateTimeOffset.MinValue;
        }

        if (reader.TokenType == JsonTokenType.Number) {
            if (reader.TryGetInt64(out var numeric)) {
                return FromUnix(numeric);
            }

            if (reader.TryGetDouble(out var numericDouble)) {
                return FromUnix((long)numericDouble);
            }
        }

        if (reader.TokenType == JsonTokenType.String) {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value)) {
                return DateTimeOffset.MinValue;
            }

            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric)) {
                return FromUnix(numeric);
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericDouble)) {
                return FromUnix((long)numericDouble);
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)) {
                return parsed;
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)) {
                return parsed;
            }
        }

        return DateTimeOffset.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) {
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }

    private static DateTimeOffset FromUnix(long value) {
        return value > 9_999_999_999L
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
    }
}
