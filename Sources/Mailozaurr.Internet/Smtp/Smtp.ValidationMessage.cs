using MimeKit.Utils;
using System.Globalization;
using System.Threading;

namespace Mailozaurr;

public partial class Smtp {
    /// <summary>
    /// Sends a neutral validation message through the specified SMTP server.
    /// </summary>
    /// <param name="server">SMTP server name.</param>
    /// <param name="port">SMTP server port.</param>
    /// <param name="request">Validation message settings.</param>
    /// <param name="secureSocketOptions">Controls SSL/TLS usage.</param>
    /// <param name="useSsl">Compatibility flag overriding <paramref name="secureSocketOptions"/> when set.</param>
    public static SmtpValidationMessageInfo SendValidationMessage(
        string server,
        int port,
        SmtpValidationMessageRequest request,
        SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto,
        bool useSsl = false) {
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        var testId = string.IsNullOrWhiteSpace(request.TestId)
            ? "Mailozaurr-SmtpValidation-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            : request.TestId!.Trim();
        var sender = request.Sender?.Trim() ?? string.Empty;
        var recipient = request.Recipient?.Trim() ?? string.Empty;
        var heloHost = string.IsNullOrWhiteSpace(request.HeloHost) ? "localhost" : request.HeloHost.Trim();
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Authorized SMTP validation " + testId
            : request.Subject!.Trim();
        var body = string.IsNullOrWhiteSpace(request.Body)
            ? BuildDefaultValidationBody(testId, sender, recipient, server, port)
            : request.Body!;

        var smtp = new Smtp();
        var messageId = MimeUtils.GenerateMessageId(server);
        SmtpResult result;
        try {
            smtp.LocalDomain = heloHost;
            smtp.UseConnectionPool = false;
            smtp.From = sender;
            smtp.To = new object[] { recipient };
            smtp.Subject = subject;
            smtp.TextBody = body;
            smtp.Priority = request.HighPriority ? MessagePriority.High : MessagePriority.Normal;
            smtp.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["X-Mailozaurr-Validation-TestId"] = testId,
                ["X-Auto-Response-Suppress"] = "All"
            };

            result = smtp.Connect(server, port, secureSocketOptions, useSsl);
            if (result.Status) {
                smtp.CreateMessage(CancellationToken.None);
                smtp.Message.MessageId = messageId;
                result = smtp.Send();
            }
        } finally {
            smtp.Dispose();
        }

        result.MessageId ??= messageId;
        return new SmtpValidationMessageInfo(server, port, testId, sender, recipient, heloHost, subject, result.MessageId, result);
    }

    private static string BuildDefaultValidationBody(string testId, string sender, string recipient, string server, int port) {
        return "Authorized SMTP mail-flow validation test." + Environment.NewLine +
            "TestId: " + testId + Environment.NewLine +
            "Header From and recipient are intentionally set to validate SPF/DKIM/DMARC and gateway handling." + Environment.NewLine +
            "From: " + sender + Environment.NewLine +
            "To: " + recipient + Environment.NewLine +
            "Direct target: " + server + ":" + port.ToString(CultureInfo.InvariantCulture) + Environment.NewLine +
            "No action is required by the recipient.";
    }
}
