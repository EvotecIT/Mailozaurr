namespace Mailozaurr;

public partial class ClientSmtp : SmtpClient {
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public List<string>? Attachments { get; set; } = new List<string>();
    public object From { get; set; }
    public IEnumerable<object>? To { get; set; } = new List<object>();
    public IEnumerable<object>? Cc { get; set; } = new List<object>();
    public IEnumerable<object>? Bcc { get; set; } = new List<object>();
    public object? ReplyTo { get; set; }
    public MimeMessage Message { get; set; }
    public MessagePriority Priority { get; set; }
    public DeliveryNotification[]? DeliveryNotificationOption { get; set; }
    public string SentTo {
        get {
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(To.Select(ConvertToMailboxAddress).SelectMany(x => x).Select(x => x.Address));
            }
            if (Cc != null) {
                addresses.AddRange(Cc.Select(ConvertToMailboxAddress).SelectMany(x => x).Select(x => x.Address));
            }
            if (Bcc != null) {
                addresses.AddRange(Bcc.Select(ConvertToMailboxAddress).SelectMany(x => x).Select(x => x.Address));
            }
            return string.Join(",", addresses);
        }
    }

    public string SentFrom {
        get {
            var addresses = ConvertToMailboxAddress(From).Select(x => x.Address);
            return string.Join(",", addresses);
        }
    }

    public ClientSmtp() { }

    public ClientSmtp(ProtocolLogger protocolLogger) : base(protocolLogger) { }

    protected override DeliveryStatusNotification? GetDeliveryStatusNotifications(MimeMessage message, MailboxAddress mailbox) {
        DeliveryStatusNotification combinedOption = 0;
        if (DeliveryNotificationOption != null) {
            foreach (var option in DeliveryNotificationOption) {
                switch (option) {
                    case DeliveryNotification.None:
                        break;
                    case DeliveryNotification.Delay:
                        combinedOption |= DeliveryStatusNotification.Delay;
                        break;
                    case DeliveryNotification.Never:
                        combinedOption |= DeliveryStatusNotification.Never;
                        break;
                    case DeliveryNotification.OnFailure:
                        combinedOption |= DeliveryStatusNotification.Failure;
                        break;
                    case DeliveryNotification.OnSuccess:
                        combinedOption |= DeliveryStatusNotification.Success;
                        break;
                }
            }
        }
        return combinedOption;
    }

    public void CreateMessage() {
        var message = new MimeMessage();
        AddAddressesToMessage(message);
        SetMessagePriority(message);
        BuildMessageBody(message);
        message.Subject = Subject;
        Message = message;
    }

    private void AddAddressesToMessage(MimeMessage message) {
        var fromAddresses = ConvertToMailboxAddress(From).ToList();
        if (fromAddresses.Any()) {
            LoggingMessages.Logger.WriteVerbose("Adding from address to message: {0}", fromAddresses.First());
            message.From.Add(fromAddresses.First());
        }

        if (To != null && To.Any()) {
            message.To.AddRange(To.SelectMany(ConvertToMailboxAddress));
        }

        if (Cc != null && Cc.Any()) {
            message.Cc.AddRange(Cc.SelectMany(ConvertToMailboxAddress));
        }

        if (Bcc != null && Bcc.Any()) {
            message.Bcc.AddRange(Bcc.SelectMany(ConvertToMailboxAddress));
        }

        if (ReplyTo != null) {
            var replyToAddresses = ConvertToMailboxAddress(ReplyTo).ToList();
            if (replyToAddresses.Any()) {
                message.ReplyTo.Add(replyToAddresses.First());
            }
        }
    }

    private void SetMessagePriority(MimeMessage message) {
        LoggingMessages.Logger.WriteVerbose("Setting message priority to {0}", Priority);
        switch (Priority) {
            case MessagePriority.High:
                message.Priority = MimeKit.MessagePriority.Urgent;
                break;
            case MessagePriority.Low:
                message.Priority = MimeKit.MessagePriority.NonUrgent;
                break;
            default:
                message.Priority = MimeKit.MessagePriority.Normal;
                break;
        }
    }

    private void BuildMessageBody(MimeMessage message) {
        var bodyBuilder = new BodyBuilder();
        if (!string.IsNullOrEmpty(HtmlBody)) {
            bodyBuilder.HtmlBody = HtmlBody;
        }
        if (!string.IsNullOrEmpty(TextBody)) {
            bodyBuilder.TextBody = TextBody;
        }
        if (Attachments != null) {
            foreach (var attachment in Attachments) {
                bodyBuilder.Attachments.Add(attachment);
            }
        }
        message.Body = bodyBuilder.ToMessageBody();
    }

    public void SaveMessage(string path) {
        Message.WriteTo(path);
    }

    private IEnumerable<MailboxAddress> ConvertToMailboxAddress(object input) {
        if (input is string str) {
            if (!str.Contains("<>")) {
                yield return new MailboxAddress(str, str);
            } else {
                yield return MailboxAddress.Parse(str);
            }
        } else if (input is IDictionary dict) {
            if (dict.Contains("Name") && dict.Contains("Email")) {
                yield return new MailboxAddress(dict["Name"]?.ToString(), dict["Email"]?.ToString());
            }
        } else if (input is MailboxAddress mailbox) {
            yield return mailbox;
        } else if (input is IEnumerable<object> list) {
            foreach (var item in list) {
                foreach (var address in ConvertToMailboxAddress(item)) {
                    yield return address;
                }
            }
        } else {
            throw new ArgumentException("Invalid input type for ConvertToMailboxAddress");
        }
    }

}