using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mailozaurr;

/// <summary>
/// Represents a mailbox permission entry returned by Microsoft Graph.
/// </summary>
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
        Raw = raw ?? new Dictionary<string, object>();
        UserPrincipalName = userPrincipalName;
        if (raw.TryGetValue("id", out var idObj)) Id = idObj as string;
        if (raw.TryGetValue("roles", out var rolesObj) && rolesObj is object[] arr)
            Roles = arr.Select(r => r?.ToString()).Where(r => r is not null).ToArray()!;
        if (raw.TryGetValue("grantedTo", out var granted)) GrantedTo = granted;
    }

    /// <summary>Unique permission identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Mailbox owning the permission.</summary>
    public string? UserPrincipalName { get; set; }

    /// <summary>Roles assigned by the permission.</summary>
    public string[]? Roles { get; set; }

    /// <summary>Information about the grantee.</summary>
    public object? GrantedTo { get; set; }

    /// <summary>Raw dictionary returned by Graph.</summary>
    public Dictionary<string, object> Raw { get; }

    /// <summary>
    /// Creates an instance from PowerShell hashtable input.
    /// </summary>
    /// <param name="table">Hashtable describing the permission.</param>
    /// <param name="userPrincipalName">Mailbox owner.</param>
    /// <returns>Created permission object.</returns>
    public static GraphMailboxPermission FromHashtable(Hashtable table, string? userPrincipalName = null) {
        var dict = table.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value);
        return new GraphMailboxPermission(dict, userPrincipalName);
    }

    /// <summary>
    /// Converts the permission to a dictionary suitable for Graph requests.
    /// </summary>
    public Dictionary<string, object> ToDictionary() {
        var dict = new Dictionary<string, object>(Raw);
        if (Id != null) dict["id"] = Id;
        if (Roles != null) dict["roles"] = Roles;
        if (GrantedTo != null) dict["grantedTo"] = GrantedTo;
        return dict;
    }

    /// <summary>
    /// Adds this permission to the associated mailbox.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    public async Task AddAsync(GraphCredential credential) {
        if (UserPrincipalName is null) throw new InvalidOperationException("UserPrincipalName not set.");
        var body = JsonSerializer.Serialize(ToDictionary());
        await MicrosoftGraphUtils.AddMailboxPermissionAsync(credential, UserPrincipalName, body).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes this permission from the associated mailbox.
    /// </summary>
    /// <param name="credential">Graph credential.</param>
    public async Task RemoveAsync(GraphCredential credential) {
        if (UserPrincipalName is null) throw new InvalidOperationException("UserPrincipalName not set.");
        if (Id is null) throw new InvalidOperationException("Id not set.");
        await MicrosoftGraphUtils.RemoveMailboxPermissionAsync(credential, UserPrincipalName, Id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override string ToString() => Id ?? base.ToString();
}
