using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a <see cref="GraphMailboxPermission"/> object.
/// </summary>
[Cmdlet(VerbsCommon.New, "GraphMailboxPermissionObject")]
[OutputType(typeof(GraphMailboxPermission))]
public sealed class CmdletNewGraphMailboxPermissionObject : PSCmdlet {
    /// <summary>
    /// User principal name of the grantee.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Params")]
    public string GrantedToUser { get; set; } = string.Empty;

    /// <summary>
    /// Roles assigned to the grantee.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public GraphMailboxRole[]? Roles { get; set; }

    /// <summary>
    /// Mailbox owner user principal name.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string? UserPrincipalName { get; set; }

    /// <summary>
    /// Permission identifier.
    /// </summary>
    [Parameter(ParameterSetName = "Params")]
    public string? Id { get; set; }

    /// <summary>
    /// Permission builder object used to create the permission.
    /// </summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Builder")]
    public GraphMailboxPermissionBuilder? Builder { get; set; }

    /// <summary>
    /// Constructs the <see cref="GraphMailboxPermission"/> from parameters or builder.
    /// </summary>
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
