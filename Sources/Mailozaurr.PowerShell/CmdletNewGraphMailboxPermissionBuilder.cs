using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphMailboxPermissionBuilder"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphMailboxPermissionBuilder")]
[OutputType(typeof(GraphMailboxPermissionBuilder))]
public sealed class CmdletNewGraphMailboxPermissionBuilder : PSCmdlet {
    /// <summary>
    /// Mailbox owner user principal name.
    /// </summary>
    [Parameter]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// User to grant permissions to.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string GrantedToUser { get; set; } = string.Empty;

    /// <summary>
    /// Roles to assign on the mailbox.
    /// </summary>
    [Parameter]
    public GraphMailboxRole[]? Roles { get; set; }

    /// <summary>
    /// Optional permission identifier.
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// Builds the <see cref="GraphMailboxPermission"/> from parameters.
    /// </summary>
    protected override void ProcessRecord() {
        var builder = new GraphMailboxPermissionBuilder();
        if (UserPrincipalName != null)
            builder.UserPrincipalName(UserPrincipalName);
        builder.GrantedToUser(GrantedToUser);
        if (Roles != null)
            builder.Roles(Roles);
        if (Id != null)
            builder.Id(Id);
        WriteObject(builder);
    }
}
