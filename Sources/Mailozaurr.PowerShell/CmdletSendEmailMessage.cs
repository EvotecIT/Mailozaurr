using System;
using System.Management.Automation;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Sends an email message using SMTP, SendGrid, or Microsoft Graph from within PowerShell. Replaces the deprecated Send-MailMessage.</para>
/// <para type="description">The Send-EmailMessage cmdlet sends an email message using a variety of providers and authentication methods. It supports SMTP (with or without SSL/TLS), SendGrid API, and Microsoft Graph API. The cmdlet allows for rich email composition, including HTML and text bodies, attachments, delivery notifications, and advanced logging. It is designed as a modern, secure, and flexible replacement for Send-MailMessage.</para>
/// <para type="description">Authentication can be provided via credentials, OAuth2, or provider-specific tokens. The cmdlet supports multiple parameter sets for compatibility with different authentication and provider scenarios.</para>
/// </summary>
/// <example>
///   <summary>Send a basic email via SMTP with credentials</summary>
///   <prefix>PS&gt; </prefix>
///   <code>Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Server 'smtp.office365.com' -Credential (Get-Credential) -HTML '<b>Hello</b>' -Subject 'Test Email'</code>
/// </example>
/// <example>
///   <summary>Send an email with an attachment and high priority</summary>
///   <prefix>PS&gt; </prefix>
///   <code>Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'Report' -Body 'See attached.' -Attachment 'C:\Reports\report.pdf' -Priority High -Server 'smtp.office365.com' -Credential (Get-Credential)</code>
/// </example>
/// <example>
///   <summary>Send an email using SendGrid API</summary>
///   <prefix>PS&gt; </prefix>
///   <code>$cred = ConvertTo-SendGridCredential -ApiKey 'YOUR_SENDGRID_KEY'
/// Send-EmailMessage -From 'john.doe@example.com' -To 'recipient@example.com' -Subject 'SendGrid Test' -Body 'Hello from SendGrid' -SendGrid -Credential $cred</code>
/// </example>
/// <example>
///   <summary>Send an email using Microsoft Graph API</summary>
///   <prefix>PS&gt; </prefix>
///   <code>$cred = ConvertTo-GraphCredential -ClientID 'CLIENT_ID' -ClientSecret 'SECRET' -DirectoryID 'TENANT_ID'
/// Send-EmailMessage -From @{ Name = 'John Doe'; Email = 'john.doe@example.com' } -To 'recipient@example.com' -Credential $cred -HTML '<b>Hello</b>' -Subject 'Graph API Email' -Graph</code>
/// </example>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
[Cmdlet(VerbsCommunications.Send, "EmailMessage", DefaultParameterSetName = "Compatibility", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[CmdletBinding()]
public sealed partial class CmdletSendEmailMessage : PSCmdlet {
    private const long GraphAttachmentLimitBytes = 150_000_000;

}
