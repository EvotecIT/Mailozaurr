using System.Threading;
using System.Threading.Tasks;
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
    public object? From { get; set; }
    /// <summary>Primary recipients.</summary>
    public IEnumerable<object>? To { get; set; } = new List<object>();
    /// <summary>Carbon copy recipients.</summary>
    public IEnumerable<object>? Cc { get; set; } = new List<object>();
    /// <summary>Blind carbon copy recipients.</summary>
    public IEnumerable<object>? Bcc { get; set; } = new List<object>();
    /// <summary>Reply-to address.</summary>
    public object? ReplyTo { get; set; }
    /// <summary>The underlying MIME message.</summary>
    public MimeMessage Message { get; set; } = new MimeMessage();
    /// <summary>Priority of the message.</summary>
    public MessagePriority Priority { get; set; }
    /// <summary>Delivery notification options.</summary>
    public DeliveryNotification[]? DeliveryNotificationOption { get; set; }
    /// <summary>Custom headers to add to the message.</summary>
    public IDictionary<string, string>? Headers { get; set; }
    /// <summary>Download remote images referenced in HtmlBody and embed them.</summary>
    public bool AutoEmbedRemoteImages { get; set; } = false;
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
            var addresses = From != null
                ? ConvertToMailboxAddress(From).Select(x => x.Address)
                : Enumerable.Empty<string>();
            return string.Join(",", addresses);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="ClientSmtp"/> class.</summary>
    public ClientSmtp() {
        Priority = MessagePriority.Normal;
    }

    /// <summary>Initializes a new instance of the <see cref="ClientSmtp"/> class using the specified logger.</summary>
    /// <param name="protocolLogger">The protocol logger.</param>
    public ClientSmtp(ProtocolLogger protocolLogger) : base(protocolLogger) {
        Priority = MessagePriority.Normal;
    }

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
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public void CreateMessage(CancellationToken cancellationToken = default) {
        InlineAttachments ??= new List<object>();
        var message = new MimeMessage();
        AddAddressesToMessage(message);
        SetMessagePriority(message);
        BuildMessageBody(message, cancellationToken);
        message.Subject = Subject;
        AddHeaders(message);
        Message = message;
    }

    private void AddAddressesToMessage(MimeMessage message) {
        var fromAddresses = From != null ? ConvertToMailboxAddress(From).ToList() : new List<MailboxAddress>();
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

    private void BuildMessageBody(MimeMessage message, CancellationToken cancellationToken) {
        var bodyBuilder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(HtmlBody)) {
            bodyBuilder.HtmlBody = HtmlBody;
        }
        if (!string.IsNullOrWhiteSpace(TextBody)) {
            bodyBuilder.TextBody = TextBody;
        }
        if (Attachments != null) {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in Attachments) {
                switch (attachment) {
                    case string path when seenPaths.Add(path):
                        if (File.Exists(path)) {
                            bodyBuilder.Attachments.Add(path);
                        } else {
                            LoggingMessages.Logger.WriteWarning(
                                $"Send-EmailMessage - File not found: {path}. Skipping attachment.");
                        }
                        break;
                    case string:
                        break;
                    case MimeEntity entity:
                        bodyBuilder.Attachments.Add(entity);
                        break;
                    case SmtpAttachmentDescriptor descriptor:
                        bodyBuilder.Attachments.Add(descriptor.CreateMimeEntity(inline: false));
                        break;
                }
            }
        }
        if (InlineAttachments != null) {
            var seenInline = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var inline in InlineAttachments) {
                MimeEntity? entity = null;
                switch (inline) {
                    case string path when seenInline.Add(path):
                    {
                        if (File.Exists(path)) {
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
                        } else {
                            LoggingMessages.Logger.WriteWarning(
                                $"Send-EmailMessage - File not found: {path}. Skipping inline attachment.");
                        }
                        break;
                    }
                    case string:
                        break;
                    case MimeEntity mime:
                        bodyBuilder.LinkedResources.Add(mime);
                        entity = mime;
                        break;
                    case SmtpAttachmentDescriptor descriptor:
                        entity = descriptor.CreateMimeEntity(inline: true);
                        bodyBuilder.LinkedResources.Add(entity);
                        break;
                }
                if (entity is MimePart inlinePart && string.IsNullOrWhiteSpace(inlinePart.ContentId)) {
                    inlinePart.ContentId = MimeUtils.GenerateMessageId();
                }
            }
        }
        if (AutoEmbedRemoteImages && !string.IsNullOrWhiteSpace(bodyBuilder.HtmlBody)) {
            var (html, images) = HtmlUtils.DownloadRemoteImagesAsync(bodyBuilder.HtmlBody, cancellationToken).GetAwaiter().GetResult();
            bodyBuilder.HtmlBody = html;
            HtmlBody = html;
            foreach (var img in images) {
                using var ms = new MemoryStream(img.Data);
                var part = new MimePart(img.MediaType) {
                    Content = new MimeContent(ms),
                    FileName = img.ContentId,
                    ContentId = img.ContentId,
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline)
                };
                bodyBuilder.LinkedResources.Add(part);
            }
        }
        message.Body = bodyBuilder.ToMessageBody();
    }

    private void AddHeaders(MimeMessage message) {
        if (Headers == null) return;
        foreach (var kvp in Headers) {
            message.Headers.Add(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Saves the constructed message to the specified file path.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    public void SaveMessage(string path) {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }
        Message.WriteTo(path);
    }

    /// <summary>
    /// Asynchronously saves the constructed message to the specified file path.
    /// </summary>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task SaveMessageAsync(string path, CancellationToken cancellationToken = default) {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory)) {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await Message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
        switch (input) {
            case string str:
                foreach (var address in ConvertStringToMailboxAddresses(str)) {
                    yield return address;
                }
                break;
            case IDictionary dict:
                foreach (var address in ConvertDictionaryToMailboxAddresses(dict)) {
                    yield return address;
                }
                break;
            case MailboxAddress mailbox:
                yield return mailbox;
                break;
            case IEnumerable<object> list:
                foreach (var address in ConvertListToMailboxAddresses(list)) {
                    yield return address;
                }
                break;
            default:
                throw new ArgumentException($"Invalid input type for ConvertToMailboxAddress: {input}");
        }
    }

    private IEnumerable<MailboxAddress> ConvertStringToMailboxAddresses(string value) {
        MailboxAddress mailbox;
        try {
            mailbox = MailboxAddress.Parse(value);
        } catch (Exception ex) {
            LoggingMessages.Logger.WriteWarning($"Failed to parse address '{value}': {ex.Message}");
            yield break;
        }

        if (value.Contains('<') || value.Contains('>')) {
            yield return mailbox;
        } else {
            yield return new MailboxAddress(string.Empty, mailbox.Address);
        }
    }

    private IEnumerable<MailboxAddress> ConvertDictionaryToMailboxAddresses(IDictionary dict) {
        if (dict.Contains("Name") && dict.Contains("Email")) {
            var name = Convert.ToString(dict["Name"]) ?? string.Empty;
            var email = Convert.ToString(dict["Email"]);
            if (!string.IsNullOrWhiteSpace(email)) {
                yield return new MailboxAddress(name, email);
            }
        }
    }

    private IEnumerable<MailboxAddress> ConvertListToMailboxAddresses(IEnumerable<object> list) {
        foreach (var item in list) {
            foreach (var address in ConvertToMailboxAddress(item)) {
                yield return address;
            }
        }
    }
}