using MimeKit.Utils;

namespace Mailozaurr;

/// <summary>
/// Extension of <see cref="SmtpClient"/> used to build and send messages.
/// </summary>
public partial class ClientSmtp : SmtpClient {
    /// <summary>Subject of the message.</summary>
    public string Subject { get; set; } = string.Empty;
    /// <summary>HTML body of the message.</summary>
    public string HtmlBody { get; set; } = string.Empty;
    /// <summary>Plain text body of the message.</summary>
    public string TextBody { get; set; } = string.Empty;
    /// <summary>Attachments to include with the message.</summary>
    public List<object>? Attachments { get; set; } = new List<object>();
    /// <summary>Inline attachments to embed in the message.</summary>
    public List<object>? InlineAttachments { get; set; } = new List<object>();
    /// <summary>The sender address.</summary>
    public object From { get; set; }
    /// <summary>Primary recipients.</summary>
    public IEnumerable<object>? To { get; set; } = new List<object>();
    /// <summary>Carbon copy recipients.</summary>
    public IEnumerable<object>? Cc { get; set; } = new List<object>();
    /// <summary>Blind carbon copy recipients.</summary>
    public IEnumerable<object>? Bcc { get; set; } = new List<object>();
    /// <summary>Reply-to address.</summary>
    public object? ReplyTo { get; set; }
    /// <summary>The underlying MIME message.</summary>
    public MimeMessage Message { get; set; }
    /// <summary>Priority of the message.</summary>
    public MessagePriority Priority { get; set; }
    /// <summary>Delivery notification options.</summary>
    public DeliveryNotification[]? DeliveryNotificationOption { get; set; }
    /// <summary>Comma separated list of all recipients.</summary>
    public string SentTo {
        get {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new List<string>();
            if (To != null) {
                addresses.AddRange(ConvertToMailboxAddressesUnique(To, seen).Select(x => x.Address));
            }
            if (Cc != null) {
                addresses.AddRange(ConvertToMailboxAddressesUnique(Cc, seen).Select(x => x.Address));
            }
            if (Bcc != null) {
                addresses.AddRange(ConvertToMailboxAddressesUnique(Bcc, seen).Select(x => x.Address));
            }
            return string.Join(",", addresses);
        }
    }

    /// <summary>The address(es) the message is sent from.</summary>
    public string SentFrom {
        get {
            var addresses = ConvertToMailboxAddress(From).Select(x => x.Address);
            return string.Join(",", addresses);
        }
    }

    public ClientSmtp() { }

    public ClientSmtp(ProtocolLogger protocolLogger) : base(protocolLogger) { }

    /// <summary>
    /// Determines which delivery status notifications should be requested for the specified recipient.
    /// </summary>
    /// <param name="message">The message being sent.</param>
    /// <param name="mailbox">The recipient mailbox address.</param>
    /// <returns>The delivery status notification flags to use, or <c>null</c>.</returns>
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

    /// <summary>
    /// Builds the <see cref="MimeMessage"/> based on the configured properties.
    /// </summary>
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

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (To != null && To.Any()) {
            message.To.AddRange(ConvertToMailboxAddressesUnique(To, seen));
        }

        if (Cc != null && Cc.Any()) {
            message.Cc.AddRange(ConvertToMailboxAddressesUnique(Cc, seen));
        }

        if (Bcc != null && Bcc.Any()) {
            message.Bcc.AddRange(ConvertToMailboxAddressesUnique(Bcc, seen));
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
                switch (attachment) {
                    case string path:
                        bodyBuilder.Attachments.Add(path);
                        break;
                    case MimeEntity entity:
                        bodyBuilder.Attachments.Add(entity);
                        break;
                }
            }
        }
        if (InlineAttachments != null) {
            foreach (var inline in InlineAttachments) {
                MimeEntity? entity = null;
                switch (inline) {
                    case string path:
                    {
                        // Read the file into memory so it can be removed immediately
                        var bytes = File.ReadAllBytes(path);
                        using var ms = new MemoryStream(bytes);
                        var part = new MimePart(MimeTypes.GetMimeType(path))
                        {
                            Content = new MimeContent(ms),
                            FileName = Path.GetFileName(path),
                            ContentId = Path.GetFileName(path),
                            ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
                        };
                        bodyBuilder.LinkedResources.Add(part);
                        entity = part;
                        break;
                    }
                    case MimeEntity mime:
                        bodyBuilder.LinkedResources.Add(mime);
                        entity = mime;
                        break;
                }
                if (entity is MimePart inlinePart && string.IsNullOrEmpty(inlinePart.ContentId)) {
                    inlinePart.ContentId = MimeUtils.GenerateMessageId();
                }
            }
        }
        message.Body = bodyBuilder.ToMessageBody();
    }

    /// <summary>
    /// Saves the constructed message to the specified file path.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    public void SaveMessage(string path) {
        Message.WriteTo(path);
    }

    private IEnumerable<MailboxAddress> ConvertToMailboxAddressesUnique(IEnumerable<object>? inputs, HashSet<string> seen) {
        if (inputs == null) yield break;
        foreach (var input in inputs) {
            foreach (var address in ConvertToMailboxAddress(input)) {
                var lowered = address.Address.ToLowerInvariant();
                if (seen.Add(lowered)) {
                    yield return address;
                }
            }
        }
    }

    private IEnumerable<MailboxAddress> ConvertToMailboxAddress(object input) {
        if (input is string str) {
            var mailbox = MailboxAddress.Parse(str);
            if (str.Contains('<') || str.Contains('>')) {
                yield return mailbox;
            } else {
                yield return new MailboxAddress(string.Empty, mailbox.Address);
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
            throw new ArgumentException($"Invalid input type for ConvertToMailboxAddress: {input}");
        }
    }

}