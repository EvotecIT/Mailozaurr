using System.Management.Automation;
using Mailozaurr;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Runspaces;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves attachments for a specific mail message via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-EmailGraphMessageAttachment</c> cmdlet retrieves attachments for the specified mail message ID and user principal name using Microsoft Graph API. Provide a <see cref="GraphConnectionInfo"/> object created with <c>Connect-EmailGraph</c> or authenticate via <c>Connect-MgGraph</c>.</para>
/// <example>
///   <summary>Get attachments for a mail message</summary>
///   <code>$cred = ConvertTo-GraphCredential -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"
///   Get-EmailGraphMessageAttachment -UserPrincipalName "user@domain.com" -MessageId "AAMk..." -Credential $cred</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate attachments for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailGraphMessageAttachment")]
[OutputType(typeof(Attachment))]
public class CmdletGetEmailGraphMessageAttachment : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the user principal name (email address) whose mail message attachments will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }
    /// <summary>
    /// <para type="description">Specifies the message ID for which attachments will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? MessageId { get; set; }
    /// <summary>
    [Parameter(ParameterSetName = "Graph")]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }


    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }
    /// <summary>
    /// <para type="description">Specifies the properties to retrieve for each attachment.</para>
    /// </summary>
    [Parameter(ParameterSetName = "Graph")]
    [Parameter(ParameterSetName = "MgGraphRequest")]
    public string[]? Property { get; set; }

    /// <summary>
    /// Retrieves attachments for the specified mail message via Microsoft Graph API.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        if (ParameterSetName == "Graph") {
            var conn = Connection ?? DefaultSessions.GraphSession;
            if (conn == null) {
                WriteWarning("Get-EmailGraphMessageAttachment - Connection not provided and no default session available.");
                return;
            }
            var attachments = await MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(conn.Credential, UserPrincipalName!, MessageId!, Property);
            foreach (var att in attachments) {
                WriteObject(att);
            }
        } else {
            var query = new Dictionary<string, object>();
            if (Property != null && Property.Length > 0) query["$select"] = string.Join(",", Property);
            var uri = MicrosoftGraphUtils.JoinUriQuery("https://graph.microsoft.com/v1.0", $"/users/{UserPrincipalName}/messages/{MessageId}/attachments", query);
            var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            ps.AddCommand("Invoke-MgGraphRequest").AddParameter("Method", "GET").AddParameter("Uri", uri);
            var results = ps.Invoke();
            foreach (var res in results) WriteObject(res);
        }
    }
}