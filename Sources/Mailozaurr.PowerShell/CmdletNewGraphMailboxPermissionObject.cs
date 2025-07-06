using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphMailboxPermission"/> object.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphMailboxPermissionObject")]
[OutputType(typeof(GraphMailboxPermission))]
public sealed class CmdletNewGraphMailboxPermissionObject : PSCmdlet {
    [Parameter(Mandatory = true, ParameterSetName = "Params")]
    public string GrantedToUser { get; set; } = string.Empty;

    [Parameter(ParameterSetName = "Params")]
    public GraphMailboxRole[]? Roles { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Params")]
    public string? Id { get; set; }

    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Builder")]
    public GraphMailboxPermissionBuilder? Builder { get; set; }

    protected override void ProcessRecord() {
        GraphMailboxPermission permission;
        if (ParameterSetName == "Builder") {
            permission = Builder!.Build();
        } else {
            permission = new GraphMailboxPermission {
                UserPrincipalName = UserPrincipalName,
                Id = Id,
                Roles = Roles,
                GrantedTo = new GraphMailboxGrantee { User = GrantedToUser }
            };
        }
        WriteObject(permission);
    }
}
