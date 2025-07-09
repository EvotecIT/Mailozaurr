using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Sends an email using the Gmail API.
/// </summary>
[Cmdlet(VerbsCommunications.Send, "GmailMessage")]
[OutputType(typeof(GmailMessage))]
public sealed class CmdletSendGmailMessage : AsyncPSCmdlet {
    [Parameter(Mandatory = true)]
    public string? GmailAccount { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNull]
    public PSCredential? Credential { get; set; }

    [Parameter(Mandatory = true)]
    public object? From { get; set; }

    [Parameter(Mandatory = true)]
    public object[]? To { get; set; }

    [Parameter]
    public string? Subject { get; set; }

    [Parameter]
    public string[]? HtmlBody { get; set; }

    [Parameter]
    public string[]? TextBody { get; set; }

    [Parameter]
    public object[]? Attachment { get; set; }

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
            Attachments = Attachment?.ToList()
        };
        try {
            smtp.CreateMessage();
            var client = new GmailApiClient(oauth);
            var result = await client.SendAsync(GmailAccount!, smtp.Message, CancelToken);
            WriteObject(result);
        } finally {
            smtp.Dispose();
        }
    }
}
