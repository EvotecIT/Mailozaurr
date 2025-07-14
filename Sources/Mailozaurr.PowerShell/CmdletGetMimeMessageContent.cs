using System.Management.Automation;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Retrieves text and HTML bodies from a MIME message.
/// </summary>
[Cmdlet(VerbsCommon.Get, "MimeMessageContent")]
[OutputType(typeof(MimeMessageContent))]
public sealed class CmdletGetMimeMessageContent : PSCmdlet {
    /// <summary>Message to extract content from.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [Alias("Message")]
    public object? InputObject { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (InputObject == null) return;
        MimeMessage? message = InputObject switch {
            MimeMessage m => m,
            ImapMessageInfo info => info.Raw.Message,
            ImapEmailMessage imap => imap.Message,
            Pop3MessageInfo pinfo => pinfo.Raw.Message,
            Pop3EmailMessage pop => pop.Message,
            GraphEmailMessage g => g.Message,
            _ => null
        };
        if (message != null) {
            WriteObject(new MimeMessageContent(message));
        }
    }
}
