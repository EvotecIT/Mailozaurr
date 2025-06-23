using System.Management.Automation;
using Mailozaurr;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves mail messages for a user via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-MailMessage</c> cmdlet retrieves mail messages for the specified user principal name (email address) using Microsoft Graph API. You can specify client credentials directly or use a PSCredential object. Supports filtering, property selection, and limiting the number of results. Returns messages as PSObjects for further automation or reporting.</para>
/// <example>
///   <summary>Get mail messages for a user</summary>
///   <code>Get-MailMessage -UserPrincipalName "user@domain.com" -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"</code>
/// </example>
/// <example>
///   <summary>Get mail messages with a filter and limit</summary>
///   <code>Get-MailMessage -UserPrincipalName "user@domain.com" -Filter "subject eq 'Test'" -Limit 10</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate mail messages for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailMessage")]
[OutputType(typeof(PSObject))]
public class CmdletGetMailMessage : AsyncPSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the user principal name (email address) whose mail messages will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? UserPrincipalName { get; set; }
    /// <summary>
    /// <para type="description">Specifies the client ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter]
    public string? ClientId { get; set; }
    /// <summary>
    /// <para type="description">Specifies the client secret for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter]
    public string? ClientSecret { get; set; }
    /// <summary>
    /// <para type="description">Specifies the directory (tenant) ID for Microsoft Graph authentication.</para>
    /// </summary>
    [Parameter]
    public string? DirectoryId { get; set; }
    /// <summary>
    /// <para type="description">Specifies the properties to retrieve for each mail message.</para>
    /// </summary>
    [Parameter]
    public string[]? Property { get; set; }
    /// <summary>
    /// <para type="description">Specifies an OData filter string to filter the mail messages.</para>
    /// </summary>
    [Parameter]
    public string? Filter { get; set; }
    /// <summary>
    /// <para type="description">Specifies the maximum number of mail messages to retrieve.</para>
    /// </summary>
    [Parameter]
    public int? Limit { get; set; }
    /// <summary>
    /// <para type="description">Specifies a PSCredential object for Microsoft Graph authentication. Username should be in the format clientid@directoryid.</para>
    /// </summary>
    [Parameter]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Retrieves mail messages for the specified user via Microsoft Graph API.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        GraphCredential cred;
        if (Credential != null) {
            // Username is clientid@directoryid
            var parts = Credential.UserName.Split('@');
            if (parts.Length == 2) {
                cred = new GraphCredential {
                    ClientId = parts[0],
                    DirectoryId = parts[1],
                    ClientSecret = Credential.GetNetworkCredential().Password
                };
            } else {
                ThrowTerminatingError(new ErrorRecord(new System.ArgumentException("Credential.UserName must be in the format clientid@directoryid"), "InvalidCredentialFormat", ErrorCategory.InvalidArgument, Credential));
                return;
            }
        } else {
            cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
        }
        var messages = await MicrosoftGraphUtils.GetMailMessagesAsync(cred, UserPrincipalName, Property, Filter, Limit);
        foreach (var dict in messages) {
            WriteObject(PSObject.AsPSObject(dict));
        }
    }
}
