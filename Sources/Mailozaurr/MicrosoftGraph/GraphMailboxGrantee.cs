using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Mailozaurr;

/// <summary>
/// Represents the entity receiving mailbox permissions.
/// </summary>
/// <remarks>
/// Only minimal details are stored for the grantee as typically
/// only the user principal name is required.
/// </remarks>
public class GraphMailboxGrantee {
    /// <summary>User principal name of the grantee.</summary>
    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>Creates an instance from a dictionary.</summary>
    /// <param name="dict">Dictionary containing grantee fields.</param>
    public static GraphMailboxGrantee FromDictionary(IDictionary<string, object?> dict) {
        var g = new GraphMailboxGrantee();
        if (dict.TryGetValue("user", out var u)) g.User = u?.ToString();
        return g;
    }

    /// <summary>Creates an instance from a PowerShell hashtable.</summary>
    /// <param name="table">Hashtable containing grantee fields.</param>
    public static GraphMailboxGrantee FromHashtable(Hashtable table) {
        var dict = table.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (object?)e.Value);
        return FromDictionary(dict);
    }

    /// <summary>Converts the grantee to a dictionary.</summary>
    public Dictionary<string, object?> ToDictionary() {
        var dict = new Dictionary<string, object?>();
        if (User != null) dict["user"] = User!;
        return dict;
    }

    /// <inheritdoc />
    public override string ToString() => User ?? base.ToString()!;
}

