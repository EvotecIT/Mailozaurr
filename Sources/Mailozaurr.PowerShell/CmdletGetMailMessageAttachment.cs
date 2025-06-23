using System.Management.Automation;
using Mailozaurr;
using System.Linq;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Retrieves attachments for a specific mail message via Microsoft Graph API.</para>
/// <para type="description">The <c>Get-MailMessageAttachment</c> cmdlet retrieves attachments for the specified mail message ID and user principal name (email address) using Microsoft Graph API. Provide a PSCredential created with <c>ConvertTo-GraphCredential</c>. Returns attachment objects for further automation or reporting.</para>
/// <example>
///   <summary>Get attachments for a mail message</summary>
///   <code>$cred = ConvertTo-GraphCredential -ClientId "id" -ClientSecret "secret" -DirectoryId "tenant"
///   Get-MailMessageAttachment -UserPrincipalName "user@domain.com" -MessageId "AAMk..." -Credential $cred</code>
/// </example>
/// <remarks>
/// Use this cmdlet to enumerate attachments for mailbox management, reporting, or migration scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailMessageAttachment")]
[OutputType(typeof(Attachment))]
public class CmdletGetMailMessageAttachment : AsyncPSCmdlet {
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
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }
    /// <summary>
    /// <para type="description">Specifies the properties to retrieve for each attachment.</para>
    /// </summary>
    [Parameter]
    public string[]? Property { get; set; }

    /// <summary>
    /// Retrieves attachments for the specified mail message via Microsoft Graph API.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var cred = MicrosoftGraphUtils.ConvertFromGraphCredential(
            Credential!.UserName,
            Credential.GetNetworkCredential().Password);
        var attachments = await MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(cred, UserPrincipalName, MessageId, Property);
        foreach (var att in attachments) {
            WriteObject(att);
        }
    }
}