using System.Diagnostics;
using System.Net.Security;

namespace Mailozaurr;

public class Smtp {
    public ClientSmtp Client { get; }

    public string Subject {
        get => Client.Subject;
        set => Client.Subject = value;
    }

    public string HtmlBody {
        get => Client.HtmlBody;
        set => Client.HtmlBody = value;
    }

    public string TextBody {
        get => Client.TextBody;
        set => Client.TextBody = value;
    }

    public List<string>? Attachments {
        get => Client.Attachments;
        set => Client.Attachments = value;
    }

    public object From {
        get => Client.From;
        set => Client.From = value;
    }

    public IEnumerable<object>? To {
        get => Client.To;
        set => Client.To = value;
    }

    public IEnumerable<object>? Cc {
        get => Client.Cc;
        set => Client.Cc = value;
    }

    public IEnumerable<object>? Bcc {
        get => Client.Bcc;
        set => Client.Bcc = value;
    }

    public object? ReplyTo {
        get => Client.ReplyTo;
        set => Client.ReplyTo = value;
    }

    public MimeMessage Message {
        get => Client.Message;
        set => Client.Message = value;
    }

    public MessagePriority Priority {
        get => Client.Priority;
        set => Client.Priority = value;
    }

    public List<DeliveryNotification>? DeliveryNotificationOption {
        get => Client.DeliveryNotificationOption;
        set => Client.DeliveryNotificationOption = value;
    }

    public int Timeout {
        get => Client.Timeout;
        set => Client.Timeout = value;
    }

    public bool CheckCertificateRevocation {
        get => Client.CheckCertificateRevocation;
        set => Client.CheckCertificateRevocation = value;
    }

    public string LocalDomain {
        get => Client.LocalDomain;
        set => Client.LocalDomain = value;
    }

    public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
        get => Client.ServerCertificateValidationCallback;
        set => Client.ServerCertificateValidationCallback = value;
    }

    public ActionPreference? ErrorAction { get; set; }

    public string SentTo => Client.SentTo;
    public string SentFrom => Client.SentFrom;

    private readonly Stopwatch stopwatch;

    public Smtp() {
        Client = new ClientSmtp();
        stopwatch = Stopwatch.StartNew();
    }

    public Smtp(ProtocolLogger protocolLogger) {
        Client = new ClientSmtp(protocolLogger);
        stopwatch = Stopwatch.StartNew();
    }

    public void CreateMessage() {
        Client.CreateMessage();
    }

    public void SaveMessage(string path) {
        Client.SaveMessage(path);
    }

    public SmtpResult Connect(string server, int port, SecureSocketOptions secureSocketOptions = SecureSocketOptions.Auto, bool useSsl = false) {
        try {
            if (useSsl) {
                // If useSsl is true, use SecureSocketOptions.StartTls, otherwise use whatever is passed in secureSocketOptions
                // Since we are maintaining backwards compatibility with Send-MailMessage, we need to use StartTls if useSsl is true
                secureSocketOptions = SecureSocketOptions.StartTls;
            }
            Client.Connect(server, port, secureSocketOptions);
            Settings.Logger.WriteVerbose($"Connected to {server} on {port} port using SSL: {secureSocketOptions}");
            return new SmtpResult(true, SentTo, SentFrom, server, port, stopwatch.Elapsed, "");
        } catch (Exception ex) {
            Settings.Logger.WriteWarning($"Send-EmailMessage - Error: {ex.Message}");
            Settings.Logger.WriteWarning($"Send-EmailMessage - Possible issue: Port? ({port} was used), Using SSL? ({secureSocketOptions}, was used). You can also try 'SkipCertificateValidation' or 'SkipCertificateRevocation'.");
            if (ErrorAction == ActionPreference.Stop) {
                throw;
            }
            return new SmtpResult(false, SentTo, SentFrom, server, port, stopwatch.Elapsed, ex.Message);
        }
    }

    public void Disconnect() {
        stopwatch.Stop();
    }

    public void Dispose() {
        stopwatch.Stop();
    }
}
