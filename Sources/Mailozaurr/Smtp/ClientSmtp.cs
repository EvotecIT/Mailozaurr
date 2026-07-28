using Mailozaurr.Definitions;
using MimeKit.Utils;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

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
    public List<AttachmentDescriptor>? Attachments { get; set; } = new List<AttachmentDescriptor>();
    /// <summary>Inline attachments to embed in the message.</summary>
    public List<AttachmentDescriptor>? InlineAttachments { get; set; } = new List<AttachmentDescriptor>();
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
    /// Returns the currently negotiated SMTP capabilities.
    /// Overridable for tests that need to fake server features.
    /// </summary>
    public virtual SmtpCapabilities GetCapabilitiesSnapshot() => Capabilities;

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
        var task = Task.Run(
            async () => await CreateMessageAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken);
        try {
            task.Wait(cancellationToken);
        } catch (AggregateException ex) when (ex.InnerExceptions.Count == 1) {
            ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
        }
    }

    /// <summary>
    /// Asynchronously builds the <see cref="MimeMessage"/> based on the configured properties.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    public async Task CreateMessageAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        InlineAttachments ??= new List<AttachmentDescriptor>();
        var message = new MimeMessage();
        AddAddressesToMessage(message);
        SetMessagePriority(message);
        await BuildMessageBodyAsync(message, cancellationToken).ConfigureAwait(false);
        message.Subject = Subject;
        AddHeaders(message);
        Message = message;
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

    private async Task BuildMessageBodyAsync(MimeMessage message, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var bodyBuilder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(HtmlBody)) {
            bodyBuilder.HtmlBody = HtmlBody;
        }
        if (!string.IsNullOrWhiteSpace(TextBody)) {
            bodyBuilder.TextBody = TextBody;
        }
        if (Attachments != null) {
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in Attachments) {
                if (descriptor == null) {
                    continue;
                }

                var path = descriptor.SourcePath;
                if (!string.IsNullOrWhiteSpace(path) && !seenPaths.Add(path!)) {
                    continue;
                }

                if (descriptor is FileAttachmentDescriptor fileDescriptor && !File.Exists(fileDescriptor.FilePath)) {
                    throw new FileNotFoundException(
                        $"Attachment '{fileDescriptor.FilePath}' was not found.",
                        fileDescriptor.FilePath);
                }

                bodyBuilder.Attachments.Add(descriptor.CreateMimeEntity(inline: false));
            }
        }
        if (InlineAttachments != null) {
            var seenInline = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in InlineAttachments) {
                if (descriptor == null) {
                    continue;
                }

                var path = descriptor.SourcePath;
                if (!string.IsNullOrWhiteSpace(path) && !seenInline.Add(path!)) {
                    continue;
                }

                if (descriptor is FileAttachmentDescriptor fileDescriptor && !File.Exists(fileDescriptor.FilePath)) {
                    throw new FileNotFoundException(
                        $"Inline attachment '{fileDescriptor.FilePath}' was not found.",
                        fileDescriptor.FilePath);
                }

                var entity = descriptor.CreateMimeEntity(inline: true);
                bodyBuilder.LinkedResources.Add(entity);

                if (entity is MimePart inlinePart && string.IsNullOrWhiteSpace(inlinePart.ContentId)) {
                    inlinePart.ContentId = MimeUtils.GenerateMessageId();
                }
            }
        }
        if (AutoEmbedRemoteImages && bodyBuilder.HtmlBody is { } htmlBody && !string.IsNullOrWhiteSpace(htmlBody)) {
            var (html, images) = await HtmlUtils.DownloadRemoteImagesAsync(htmlBody, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            bodyBuilder.HtmlBody = html;
            HtmlBody = html;
            foreach (var img in images) {
                using var ms = new MemoryStream(img.Data);
                var part = new MimePart(img.MediaType) {
                    Content = new MimeContent(ms),
                    FileName = img.ContentId,
                    ContentId = img.ContentId,
                    ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                    ContentTransferEncoding = ContentEncoding.Base64
                };
                bodyBuilder.LinkedResources.Add(part);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
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

}
