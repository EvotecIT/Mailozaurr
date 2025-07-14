using System.Management.Automation;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves a MIME representation of a Graph mail message.
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailGraphMessageMime")]
[OutputType(typeof(GraphEmailMessage))]
public sealed class CmdletGetEmailGraphMessageMime : AsyncPSCmdlet {
    /// <summary>Message info from Get-EmailGraphMessage.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = "Info")]
    public GraphMessageInfo? MessageInfo { get; set; }

    /// <summary>User principal name owning the message.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "ById")]
    public string? UserPrincipalName { get; set; }

    /// <summary>Identifier of the message.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "ById")]
    public string? MessageId { get; set; }

    /// <summary>Graph connection.</summary>
    [Parameter]
    public GraphConnectionInfo? Connection { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Get-EmailGraphMessageMime - Connection not provided and no default session available.");
            return;
        }

        var upn = ParameterSetName == "Info" ? MessageInfo?.UserPrincipalName : UserPrincipalName;
        var id = ParameterSetName == "Info" ? MessageInfo?.Id : MessageId;
        if (string.IsNullOrEmpty(upn) || string.IsNullOrEmpty(id)) {
            WriteWarning("Get-EmailGraphMessageMime - Message id or user principal name missing.");
            return;
        }

        var mime = await MicrosoftGraphUtils.GetMailMessageMimeAsync(conn.Credential, upn!, id!);
        WriteObject(new GraphEmailMessage(id!, mime));
    }
}

