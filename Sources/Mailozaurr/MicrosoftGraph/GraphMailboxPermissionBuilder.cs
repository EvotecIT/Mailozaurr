using System;

namespace Mailozaurr;

/// <summary>
/// Provides a fluent API for building <see cref="GraphMailboxPermission"/> objects.
/// </summary>
public sealed class GraphMailboxPermissionBuilder {
    private readonly GraphMailboxPermission _permission = new();

    /// <summary>Sets the permission identifier.</summary>
    public GraphMailboxPermissionBuilder Id(string id) {
        _permission.Id = id;
        return this;
    }

    /// <summary>Sets the mailbox owner UPN.</summary>
    public GraphMailboxPermissionBuilder UserPrincipalName(string upn) {
        _permission.UserPrincipalName = upn;
        return this;
    }

    /// <summary>Sets the roles for the permission.</summary>
    public GraphMailboxPermissionBuilder Roles(params GraphMailboxRole[] roles) {
        _permission.Roles = roles;
        return this;
    }

    /// <summary>Sets the grantee user.</summary>
    public GraphMailboxPermissionBuilder GrantedToUser(string user) {
        _permission.GrantedTo = new GraphMailboxGrantee { User = user };
        return this;
    }

    /// <summary>Builds the permission object.</summary>
    public GraphMailboxPermission Build() => _permission;
}