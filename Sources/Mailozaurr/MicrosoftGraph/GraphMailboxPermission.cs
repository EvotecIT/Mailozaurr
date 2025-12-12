using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Represents a mailbox permission entry returned by Microsoft Graph.
/// </summary>
/// <remarks>
/// Provides parsing helpers to convert between Graph responses and
/// strongly typed permission data.
/// </remarks>
public class GraphMailboxPermission {
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMailboxPermission"/> class.
    /// </summary>
    public GraphMailboxPermission() {
        Raw = new Dictionary<string, object>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphMailboxPermission"/> class from a dictionary.
    /// </summary>
    /// <param name="raw">Dictionary with Graph permission fields.</param>
    /// <param name="userPrincipalName">Mailbox owner.</param>
    public GraphMailboxPermission(Dictionary<string, object> raw, string? userPrincipalName = null) {
        var source = raw ?? new Dictionary<string, object>();
        Raw = source;
        UserPrincipalName = userPrincipalName;
        if (source.TryGetValue("id", out var idObj)) Id = idObj as string;
        if (source.TryGetValue("roles", out var rolesObj) && rolesObj is object[] arr)
            Roles = arr.Select(r => Enum.TryParse<GraphMailboxRole>(r?.ToString(), ignoreCase: true, out var role) ? role : GraphMailboxRole.Custom).ToArray();
        if (source.TryGetValue("grantedTo", out var granted) && granted is Dictionary<string, object> gdict)
            GrantedTo = GraphMailboxGrantee.FromDictionary(gdict);
    }

    /// <summary>Unique permission identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Mailbox owning the permission.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>Roles assigned by the permission.</summary>
    public GraphMailboxRole[]? Roles { get; set; }

    /// <summary>Information about the grantee.</summary>
    public GraphMailboxGrantee? GrantedTo { get; set; }

    /// <summary>Raw dictionary returned by Graph.</summary>
    public Dictionary<string, object> Raw { get; }

    /// <summary>
    /// Creates an instance from PowerShell hashtable input.
    /// </summary>
    /// <param name="table">Hashtable describing the permission.</param>
    /// <param name="userPrincipalName">Mailbox owner.</param>
    /// <returns>Created permission object.</returns>
    public static GraphMailboxPermission FromHashtable(Hashtable table, string? userPrincipalName = null) {
        var dict = table.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (object)(e.Value ?? string.Empty));
        return new GraphMailboxPermission(dict, userPrincipalName);
    }

    // Intentionally no overload with nullable dictionary to avoid nullability warnings in callers.

    /// <summary>
    /// Converts the permission to a dictionary suitable for Graph requests.
    /// </summary>
    public Dictionary<string, object> ToDictionary() {
        var dict = new Dictionary<string, object>(Raw);
        if (Id != null) dict["id"] = Id;
        if (Roles != null)
            dict["roles"] = Roles.Select(r => r.ToString().ToLowerInvariant()).ToArray();
        if (GrantedTo != null)
            dict["grantedTo"] = GrantedTo.ToDictionary();
        return dict;
    }

    /// <summary>
    /// Adds this permission to the associated mailbox.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    public async Task AddAsync(GraphCredential credential) {
        await AddAsync(credential, dryRun: false).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds this permission to the associated mailbox, optionally simulating the change.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    /// <param name="dryRun">When set, no Graph request is issued.</param>
    public async Task AddAsync(GraphCredential credential, bool dryRun) {
        if (UserPrincipalName is null) throw new InvalidOperationException("UserPrincipalName not set.");
        var body = JsonSerializer.Serialize(ToDictionary(), MailozaurrJsonContext.Default.DictionaryStringObject);
        await MicrosoftGraphUtils.AddMailboxPermissionAsync(credential, UserPrincipalName, body, dryRun).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes this permission from the associated mailbox.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    public async Task RemoveAsync(GraphCredential credential) {
        await RemoveAsync(credential, dryRun: false).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes this permission from the associated mailbox, optionally simulating the change.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    /// <param name="dryRun">When set, no Graph request is issued.</param>
    public async Task RemoveAsync(GraphCredential credential, bool dryRun) {
        if (UserPrincipalName is null) throw new InvalidOperationException("UserPrincipalName not set.");
        if (Id is null) throw new InvalidOperationException("Id not set.");
        await MicrosoftGraphUtils.RemoveMailboxPermissionAsync(credential, UserPrincipalName, Id, dryRun).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override string ToString() => Id ?? string.Empty;
}
