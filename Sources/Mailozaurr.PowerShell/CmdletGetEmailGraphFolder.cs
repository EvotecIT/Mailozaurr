using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Mailozaurr;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves mail folders for a user via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-EmailGraphFolder</c> cmdlet retrieves mail folders for the specified user principal name using Microsoft Graph API. Provide a <see cref="GraphConnectionInfo"/> object created with <c>Connect-EmailGraph</c> or authenticate via <c>Connect-MgGraph</c>.</para>
/// <example>
///   <summary>Get mail folders using application permissions</summary>
///   <code>$cred = ConvertTo-GraphCredential -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"
///   $graph = Connect-EmailGraph -Credential $cred
///   Get-EmailGraphFolder -UserPrincipalName "user@domain.com" -Connection $graph</code>
/// </example>
/// <example>
///   <summary>Get mail folders using Connect-MgGraph</summary>
///   <code>Connect-MgGraph -Scopes Mail.Read -NoWelcome
///   Get-EmailGraphFolder -UserPrincipalName "user@domain.com" -MgGraphRequest</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate mail folders for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "EmailGraphFolder")]
[OutputType(typeof(object))]
public class CmdletGetEmailGraphFolder : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the user principal name (email address) whose mail folders will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true, ParameterSetName = "Graph")]
    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }

    [Parameter(ParameterSetName = "Graph", ValueFromPipeline = true)]
    [ValidateNotNull]
    public GraphConnectionInfo? Connection { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "MgGraphRequest")]
    public SwitchParameter MgGraphRequest { get; set; }

    [Parameter]
    public int TimeoutSeconds { get; set; } = 100;

    [Parameter]
    public int MaxConcurrentRequests { get; set; } = 5;

    [Parameter]
    public int RetryCount { get; set; } = 0;

    [Parameter]
    public int RetryDelayMilliseconds { get; set; } = 0;

    /// <summary>
    /// Retrieves mail folders for the specified user via Microsoft Graph API.
    /// </summary>
    protected override Task ProcessRecordAsync() {
        if (ParameterSetName == "MgGraphRequest") {
            return ProcessMgGraph();
        }

        var conn = Connection ?? DefaultSessions.GraphSession;
        if (conn == null) {
            WriteWarning("Get-EmailGraphFolder - Connection not provided and no default session available.");
            return Task.CompletedTask;
        }

        return ProcessGraphAsync(conn.Credential);
    }

    private async Task ProcessGraphAsync(GraphCredential cred) {
        MicrosoftGraphUtils.TimeoutSeconds = TimeoutSeconds;
        MicrosoftGraphUtils.MaxConcurrentRequests = MaxConcurrentRequests;
        int attempts = 0;
        Exception? lastException = null;
        do {
            try {
                var folders = await MicrosoftGraphUtils.GetMailFoldersAsync(cred, UserPrincipalName!);
                foreach (var folder in folders) {
                    WriteObject(folder);
                }
                return;
            } catch (Exception ex) {
                lastException = ex;
                WriteWarning($"Get-EmailGraphFolder - {ex.Message}");
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
    }

    private Task ProcessMgGraph() {
        var uri = MicrosoftGraphUtils.BuildGraphUri(
            GraphEndpoint.V1,
            $"/users/{UserPrincipalName}/mailFolders");
        var ps = System.Management.Automation.PowerShell.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand("Invoke-MgGraphRequest")
            .AddParameter("Method", "GET")
            .AddParameter("Uri", uri);
        var results = ps.Invoke();
        foreach (var res in results) WriteObject(res);
        return Task.CompletedTask;
    }
}
