using System.Management.Automation;
using Mailozaurr;
using System.Linq;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves attachments for a specific mail message via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-MailMessageAttachment</c> cmdlet retrieves attachments for the specified mail message ID and user principal name (email address) using Microsoft Graph API. You can specify client credentials directly. Returns attachment objects for further automation or reporting.</para>
/// <example>
///   <summary>Get attachments for a mail message</summary>
///   <code>Get-MailMessageAttachment -UserPrincipalName "user@domain.com" -MessageId "AAMk..." -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate attachments for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailMessageAttachment")]
[OutputType(typeof(Attachment))]
public class CmdletGetMailMessageAttachment : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the user principal name (email address) whose mail message attachments will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? UserPrincipalName { get; set; }
    /// <summary>
    /// <para type="description">Specifies the message ID for which attachments will be retrieved.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? MessageId { get; set; }
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
    /// <para type="description">Specifies the properties to retrieve for each attachment.</para>
    /// </summary>
    [Parameter]
    public string[]? Property { get; set; }

    /// <summary>
    /// Retrieves attachments for the specified mail message via Microsoft Graph API.
    /// </summary>
    protected override void ProcessRecord() {
        var cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
        var task = MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(cred, UserPrincipalName, MessageId, Property);
        task.Wait();
        foreach (var att in task.Result) {
            WriteObject(att);
        }
    }
}