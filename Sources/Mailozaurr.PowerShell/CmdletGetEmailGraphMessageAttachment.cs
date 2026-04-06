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
///   <code>$graph = Connect-EmailGraph -ClientId "id" -DirectoryId "tenant" -ClientSecretSecretName "graph-client-secret" -ClientSecretVaultName "MailSecrets"
///   Get-EmailGraphMessageAttachment -UserPrincipalName "user@domain.com" -MessageId "AAMk..." -Connection $graph</code>
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
    /// Graph connection information.
    /// </summary>
    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }


    /// <summary>
    /// Executes the request via Invoke-MgGraphRequest when set.
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Maximum number of concurrent Graph requests.
    /// </summary>
    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Number of retry attempts on failure.
    /// </summary>
    [Parameter]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Delay between retries in milliseconds.
    /// </summary>
    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;
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
            MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
            MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
            int attempts = 0;
            Exception? lastException = null;
            do {
                try {
                    var attachments = await MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(conn.Credential, UserPrincipalName!, MessageId!, Property);
                    foreach (var att in attachments) {
                        WriteObject(att);
                    }
                    return;
                } catch (Exception ex) {
                    lastException = ex;
                    WriteWarning($"Get-EmailGraphMessageAttachment - {ex.Message}");
                    if (!Helpers.IsTransient(ex) || attempts >= RetryCount) {
                        if (ex is GraphApiException gex) {
                            WriteError(new ErrorRecord(gex, "GraphApiError", ErrorCategory.InvalidOperation, null));
                        } else {
                            WriteError(new ErrorRecord(ex, "GraphError", ErrorCategory.InvalidOperation, null));
                        }
                        return;
                    }
                    if (RetryDelayMilliseconds > 0) {
                        await Task.Delay(RetryDelayMilliseconds);
                    }
                }
                attempts++;
            } while (attempts <= RetryCount);
            if (lastException is not null) {
                WriteError(new ErrorRecord(lastException, "GraphError", ErrorCategory.InvalidOperation, null));
            }
        } else {
            var query = new Dictionary<string, object>();
            if (Property != null && Property.Length > 0) query["$select"] = string.Join(",", Property);
            var uri = MicrosoftGraphUtils.JoinUriQuery(
                GraphEndpoint.V1,
                $"/users/{UserPrincipalName}/messages/{MessageId}/attachments",
                query);
            var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
            ps.AddCommand("Invoke-MgGraphRequest").AddParameter("Method", "GET").AddParameter("Uri", uri);
            var results = ps.Invoke();
            foreach (var res in results) WriteObject(res);
        }
    }
}
