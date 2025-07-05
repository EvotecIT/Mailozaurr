using System;
using System.Collections.Generic;

namespace Mailozaurr;

/// <summary>
/// Provides a convenient view over a Graph email message.
/// </summary>
public class GraphMessageInfo {
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMessageInfo"/> class.
    /// </summary>
    public GraphMessageInfo() {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMessageInfo"/> class.
    /// </summary>
    /// <param name="raw">Dictionary representing the Graph message.</param>
    /// <param name="userPrincipalName">Mailbox owner.</param>
    /// <param name="summary">Optional search result snippet.</param>
    public GraphMessageInfo(Dictionary<string, object> raw, string? userPrincipalName = null, string? summary = null) {
        Raw = raw ?? throw new ArgumentNullException(nameof(raw));
        UserPrincipalName = userPrincipalName;
        Summary = summary;
        if (raw.TryGetValue("id", out var idObj)) Id = idObj as string;
        if (raw.TryGetValue("subject", out var subjectObj)) Subject = subjectObj as string;
        if (raw.TryGetValue("from", out var fromObj)) From = ExtractAddress(fromObj);
        if (raw.TryGetValue("toRecipients", out var toObj)) To = ExtractAddresses(toObj);
        if (raw.TryGetValue("sentDateTime", out var dateObj) && DateTimeOffset.TryParse(dateObj?.ToString(), out var dt)) {
            Date = dt.DateTime;
        }
    }

    /// <summary>The dictionary returned from Microsoft Graph.</summary>
    public Dictionary<string, object>? Raw { get; }

    /// <summary>User principal name of the mailbox containing the message.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>Unique identifier of the message.</summary>
    public string? Id { get; set; }

    /// <summary>Sender address.</summary>
    public string? From { get; set; }

    /// <summary>Recipient addresses.</summary>
    public string? To { get; set; }

    /// <summary>Subject of the message.</summary>
    public string? Subject { get; set; }

    /// <summary>Date the message was sent.</summary>
    public DateTime? Date { get; set; }

    /// <summary>Snippet extracted by the search service highlighting the match.</summary>
    public string? Summary { get; set; }

    /// <inheritdoc />
    public override string ToString() => Subject ?? base.ToString();

    private static string? ExtractAddress(object obj) {
        if (obj is Dictionary<string, object> dict &&
            dict.TryGetValue("emailAddress", out var emailObj) &&
            emailObj is Dictionary<string, object> email &&
            email.TryGetValue("address", out var addr)) {
            return addr as string;
        }
        return null;
    }

    private static string? ExtractAddresses(object obj) {
        if (obj is object[] arr) {
            var list = new List<string>();
            foreach (var item in arr) {
                var addr = ExtractAddress(item);
                if (addr != null) list.Add(addr);
            }
            return string.Join(", ", list);
        }
        return null;
    }
}
