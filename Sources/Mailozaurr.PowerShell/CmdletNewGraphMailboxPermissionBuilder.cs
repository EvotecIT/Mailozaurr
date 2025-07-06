using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphMailboxPermissionBuilder"/> instance.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphMailboxPermissionBuilder")]
[OutputType(typeof(GraphMailboxPermissionBuilder))]
public sealed class CmdletNewGraphMailboxPermissionBuilder : PSCmdlet {
    [Parameter]
    public string? UserPrincipalName { get; set; }

    [Parameter(Mandatory = true)]
    public string GrantedToUser { get; set; } = string.Empty;

    [Parameter]
    public GraphMailboxRole[]? Roles { get; set; }

    [Parameter]
    public string? Id { get; set; }

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
