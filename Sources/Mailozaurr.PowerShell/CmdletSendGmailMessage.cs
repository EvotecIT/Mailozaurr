using System;
using System.Collections;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using MimeKit;
using Mailozaurr;
using Mailozaurr.Definitions;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sends an email using the Gmail API.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "GmailMessage", SupportsShouldProcess = true)]
[OutputType(typeof(GmailMessage))]
public sealed class CmdletSendGmailMessage : AsyncPSCmdlet {
    /// <summary>
    /// Gmail account used to send the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    /// <summary>
    /// OAuth credential used for authentication.
    /// </summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    /// <summary>
    /// Address used in the From header.
    /// </summary>
    [Parameter(Mandatory = true)]
    public object? From { get; set; }

    /// <summary>
    /// Recipients of the message.
    /// </summary>
    [Parameter(Mandatory = true)]
    public object[]? To { get; set; }

    /// <summary>
    /// Subject line for the email message.
    /// </summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>
    /// HTML body content of the message.
    /// </summary>
    [Parameter]
    public string[]? HtmlBody { get; set; }

    /// <summary>
    /// Plain text body content of the message.
    /// </summary>
    [Parameter]
    public string[]? TextBody { get; set; }

    /// <summary>
    /// Attachments to include with the message.
    /// </summary>
    [Parameter]
    public object[]? Attachment { get; set; }

    /// <summary>
    /// Custom headers to include with the message.
    /// </summary>
    [Parameter]
    public Hashtable? Headers { get; set; }

    /// <summary>
    /// Executes the cmdlet logic asynchronously.
    /// </summary>
    protected override async Task ProcessRecordAsync() {
        var net = Credential!.GetNetworkCredential();
        var oauth = new OAuthCredential {
            UserName = net.UserName,
            AccessToken = net.Password,
            ExpiresOn = System.DateTimeOffset.MaxValue
        };

        var smtp = new Smtp {
            From = From!,
            To = To,
            Subject = Subject ?? string.Empty,
            HtmlBody = HtmlBody is null ? string.Empty : string.Join(System.Environment.NewLine, HtmlBody),
            TextBody = TextBody is null ? string.Empty : string.Join(System.Environment.NewLine, TextBody),
            Attachments = ConvertAttachments(Attachment)
        };
        if (Headers != null) {
            smtp.Headers = Headers.Cast<DictionaryEntry>()
                .ToDictionary(d => d.Key?.ToString() ?? string.Empty, d => d.Value?.ToString() ?? string.Empty);
        }
        try {
            smtp.CreateMessage();
            var client = new GmailApiClient(oauth);
            if (ShouldProcess(GmailAccount!, "Sending email message via Gmail API")) {
                var result = await client.SendAsync(GmailAccount!, smtp.Message, CancelToken);
                WriteObject(result);
            } else {
                WriteObject(new SmtpResult(false, EmailAction.Send, smtp.SentTo, smtp.SentFrom, "GmailApi", 0, smtp.Stopwatch.Elapsed, string.Empty, "Email not sent (WhatIf)"));
            }
        } finally {
            smtp.Dispose();
        }
    }
    private static List<AttachmentDescriptor>? ConvertAttachments(object[]? attachments)
    {
        if (attachments == null)
        {
            return null;
        }

        var result = new List<AttachmentDescriptor>();
        foreach (var entry in attachments)
        {
            if (entry == null)
            {
                continue;
            }

            switch (entry)
            {
                case AttachmentDescriptor descriptor:
                    result.Add(descriptor);
                    break;
                case string path:
                    result.Add(new FileAttachmentDescriptor(path));
                    break;
                case System.IO.FileInfo fileInfo:
                    result.Add(new FileAttachmentDescriptor(fileInfo.FullName));
                    break;
                default:
                    throw new ArgumentException($"Unsupported attachment type: {entry.GetType().Name}");
            }
        }

        return result;
    }
}
