using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a MIME message without requiring callers to construct MimeKit types directly.
/// </summary>
[Cmdlet(VerbsCommon.New, "MimeMessage")]
[OutputType(typeof(MimeMessage))]
public sealed class CmdletNewMimeMessage : PSCmdlet {
    /// <summary>Sender address.</summary>
    [Parameter]
    public string? From { get; set; }

    /// <summary>Recipient addresses.</summary>
    [Parameter]
    public string[]? To { get; set; }

    /// <summary>Carbon-copy recipient addresses.</summary>
    [Parameter]
    public string[]? Cc { get; set; }

    /// <summary>Blind-carbon-copy recipient addresses.</summary>
    [Parameter]
    public string[]? Bcc { get; set; }

    /// <summary>Message subject.</summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>Plain-text message body.</summary>
    [Parameter]
    public string? TextBody { get; set; }

    /// <summary>HTML message body.</summary>
    [Parameter]
    public string? HtmlBody { get; set; }

    /// <summary>File attachments to add to the message.</summary>
    [Parameter]
    public string[]? AttachmentPath { get; set; }

    /// <summary>Additional message headers.</summary>
    [Parameter]
    public Hashtable? Header { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        var message = new MimeMessage();
        AddAddress(message.From, From);
        AddAddresses(message.To, To);
        AddAddresses(message.Cc, Cc);
        AddAddresses(message.Bcc, Bcc);

        if (!string.IsNullOrWhiteSpace(Subject)) {
            message.Subject = Subject!;
        }

        if (Header != null) {
            foreach (DictionaryEntry entry in Header) {
                var name = entry.Key?.ToString();
                var value = entry.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(name) && value != null) {
                    message.Headers[name!] = value;
                }
            }
        }

        var builder = new BodyBuilder {
            TextBody = TextBody,
            HtmlBody = HtmlBody
        };

        if (AttachmentPath != null) {
            foreach (var path in AttachmentPath) {
                if (!string.IsNullOrWhiteSpace(path)) {
                    builder.Attachments.Add(path);
                }
            }
        }

        message.Body = builder.ToMessageBody();
        WriteObject(message);
    }

    private static void AddAddress(InternetAddressList list, string? address) {
        if (!string.IsNullOrWhiteSpace(address)) {
            list.Add(MailboxAddress.Parse(address!));
        }
    }

    private static void AddAddresses(InternetAddressList list, string[]? addresses) {
        if (addresses == null) {
            return;
        }

        foreach (var address in addresses) {
            AddAddress(list, address);
        }
    }
}
