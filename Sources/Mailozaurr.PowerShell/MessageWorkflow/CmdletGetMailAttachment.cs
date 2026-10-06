using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Lists or filters a message's attachment descriptions.</para>
/// <para type="description">Includes regular files, inline resources and hidden attachments by default. The command reads metadata already present on the message and does not reopen the source or save bytes.</para>
/// <example><summary>Find inline images</summary><code>$mail | Get-MailAttachment -InlineOnly -ContentType 'image/*'</code></example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailAttachment")]
[OutputType(typeof(EmailMessageAttachment))]
public sealed class CmdletGetMailAttachment : MailAttachmentCmdletBase {
    /// <summary>Writes matching attachment descriptions.</summary>
    protected override Task ProcessRecordAsync() {
        foreach (EmailMessageAttachment attachment in GetAttachments()) WriteObject(attachment);
        return Task.CompletedTask;
    }
}
