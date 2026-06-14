using System.Globalization;
using System.Text.Json;

namespace Mailozaurr;

public sealed partial class GraphApiClient {
    private static string BuildUserSegment(string userId) {
        var u = (userId ?? string.Empty).Trim();
        if (u.Length == 0 || u.Equals("me", StringComparison.OrdinalIgnoreCase)) {
            return "me";
        }
        return "users/" + Uri.EscapeDataString(u);
    }

    private static int ClampInt(int value, int min, int max) {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static string EscapeODataStringLiteral(string value) => value.Replace("'", "''");

    private static string? TryGetString(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.String) {
            return null;
        }
        var s = el.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static int? TryGetInt(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Number) {
            return null;
        }
        return el.TryGetInt32(out var v) ? v : null;
    }

    private static bool? TryGetBool(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el)) {
            return null;
        }
        if (el.ValueKind == JsonValueKind.True) {
            return true;
        }
        if (el.ValueKind == JsonValueKind.False) {
            return false;
        }
        return null;
    }

    private static DateTimeOffset? TryGetDateTimeOffset(JsonElement obj, string propertyName) {
        var s = TryGetString(obj, propertyName);
        if (string.IsNullOrWhiteSpace(s)) {
            return null;
        }
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    private static GraphEmailAddress? TryParseEmailAddress(JsonElement recipient) {
        if (recipient.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!recipient.TryGetProperty("emailAddress", out var emailAddress) || emailAddress.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!emailAddress.TryGetProperty("address", out var addressEl) || addressEl.ValueKind != JsonValueKind.String) {
            return null;
        }
        var address = addressEl.GetString();
        if (address == null) {
            return null;
        }
        address = address.Trim();
        if (address.Length == 0) {
            return null;
        }
        return new GraphEmailAddress { Email = new GraphEmail { Address = address } };
    }

    private static List<GraphEmailAddress>? TryParseEmailAddressList(JsonElement obj, string propertyName) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        if (!obj.TryGetProperty(propertyName, out var el) || el.ValueKind != JsonValueKind.Array) {
            return null;
        }
        var list = new List<GraphEmailAddress>();
        foreach (var item in el.EnumerateArray()) {
            var addr = TryParseEmailAddress(item);
            if (addr != null) {
                list.Add(addr);
            }
        }
        return list.Count == 0 ? null : list;
    }

    private static GraphMailMessage? TryParseMailMessage(JsonElement obj) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        var idRaw = TryGetString(obj, "id");
        if (idRaw == null) {
            return null;
        }
        var id = idRaw.Trim();
        if (id.Length == 0) {
            return null;
        }
        var msg = new GraphMailMessage {
            Id = id,
            Subject = TryGetString(obj, "subject"),
            ReceivedDateTime = TryGetDateTimeOffset(obj, "receivedDateTime"),
            InternetMessageId = TryGetString(obj, "internetMessageId"),
            HasAttachments = TryGetBool(obj, "hasAttachments"),
            IsRead = TryGetBool(obj, "isRead"),
            ConversationId = TryGetString(obj, "conversationId")
        };

        if (obj.TryGetProperty("from", out var fromEl)) {
            msg.From = TryParseEmailAddress(fromEl);
        }
        msg.ToRecipients = TryParseEmailAddressList(obj, "toRecipients");

        if (obj.TryGetProperty("flag", out var flagEl) && flagEl.ValueKind == JsonValueKind.Object) {
            var status = TryGetString(flagEl, "flagStatus");
            if (status != null) {
                var trimmed = status.Trim();
                if (trimmed.Length > 0) {
                    msg.Flag = new GraphMailMessageFlag { FlagStatus = trimmed };
                }
            }
        }
        return msg;
    }

    private static GraphMailFolder? TryParseMailFolder(JsonElement obj) {
        if (obj.ValueKind != JsonValueKind.Object) {
            return null;
        }
        var idRaw = TryGetString(obj, "id");
        if (idRaw == null) {
            return null;
        }
        var id = idRaw.Trim();
        if (id.Length == 0) {
            return null;
        }
        return new GraphMailFolder {
            Id = id,
            DisplayName = (TryGetString(obj, "displayName") ?? string.Empty).Trim(),
            ParentFolderId = TryGetString(obj, "parentFolderId")?.Trim(),
            ChildFolderCount = TryGetInt(obj, "childFolderCount"),
            WellKnownName = TryGetString(obj, "wellKnownName")?.Trim(),
            TotalItemCount = TryGetInt(obj, "totalItemCount"),
            UnreadItemCount = TryGetInt(obj, "unreadItemCount")
        };
    }
}