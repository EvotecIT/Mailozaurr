using OfficeIMO.Email.Data;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Closes an imported email-data artifact and its owned resources.</para>
/// <para type="description">Disposes the result returned by Import-MailData, closing PST/OST/OLM/Mbox/mailbox-directory or OAB sessions and releasing file-backed email attachment content.</para>
/// <example>
///   <summary>Close an imported archive</summary>
///   <code>$data | Close-MailData</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Close, "MailData")]
public sealed class CmdletCloseMailData : MailStoreCmdletBase {
    /// <summary>The owner result returned by Import-MailData.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public EmailDataOpenResult? InputObject { get; set; }

    /// <summary>Releases the current artifact owner.</summary>
    protected override Task ProcessRecordAsync() {
        InputObject?.Dispose();
        InputObject = null;
        return Task.CompletedTask;
    }
}
