using System.Management.Automation;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves mail folders for a user via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-MailFolder</c> cmdlet retrieves mail folders for the specified user principal name (email address) using Microsoft Graph API. You can specify client credentials directly or use a PSCredential object. Returns folder objects for use in further automation or reporting.</para>
/// <example>
///   <summary>Get mail folders for a user</summary>
///   <code>Get-MailFolder -UserPrincipalName "user@domain.com" -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate mail folders for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailFolder")]
[OutputType(typeof(object))]
public class CmdletGetMailFolder : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the user principal name (email address) whose mail folders will be retrieved.</para>
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
    /// Retrieves mail folders for the specified user via Microsoft Graph API.
    /// </summary>
    protected override void ProcessRecord() {
        var cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
        var task = MicrosoftGraphUtils.GetMailFoldersAsync(cred, UserPrincipalName);
        task.Wait();
        foreach (var folder in task.Result) {
            WriteObject(folder);
        }
    }
}
