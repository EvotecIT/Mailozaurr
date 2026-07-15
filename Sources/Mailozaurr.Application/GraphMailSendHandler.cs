using MimeKit;
using MimeKit.Utils;
using System.Globalization;
using System.IO;

namespace Mailozaurr.Application;

/// <summary>
/// Normalized Graph send handler backed by Mailozaurr Graph helpers.
/// </summary>
public sealed class GraphMailSendHandler : IMailSendHandler {
    private readonly IGraphSessionFactory _sessionFactory;
    private readonly IDraftMimeMessageFactory _draftMimeMessageFactory;
    private readonly IPendingMessageRepository? _pendingMessageRepository;
    private readonly Func<GraphSession, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<GraphMessage>> _sendAsync;

    /// <summary>
    /// Creates a new Graph send handler.
    /// </summary>
    public GraphMailSendHandler(
        IGraphSessionFactory sessionFactory,
        IDraftMimeMessageFactory? draftMimeMessageFactory = null,
        IPendingMessageRepository? pendingMessageRepository = null,
        Func<GraphSession, MailProfile, SendMessageRequest, MimeMessage, CancellationToken, Task<GraphMessage>>? sendAsync = null) {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _draftMimeMessageFactory = draftMimeMessageFactory ?? new DraftMimeMessageFactory();
        _pendingMessageRepository = pendingMessageRepository;
        _sendAsync = sendAsync ?? DefaultSendAsync;
    }

    /// <inheritdoc />
    public MailProfileKind Kind => MailProfileKind.Graph;

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) {
        if (profile == null) {
            throw new ArgumentNullException(nameof(profile));
        }
        if (request == null) {
            throw new ArgumentNullException(nameof(request));
        }

        using var session = await _sessionFactory.ConnectAsync(profile, cancellationToken).ConfigureAwait(false);
        var message = await _draftMimeMessageFactory.CreateAsync(profile, request.Message, cancellationToken).ConfigureAwait(false);
        if (request.NotBefore.HasValue) {
            if (_pendingMessageRepository == null) {
                throw new NotSupportedException("Queue-backed Graph sends require a pending-message repository.");
            }

            var queuedRecord = await QueueMessageAsync(
                profile,
                session,
                message,
                request.NotBefore,
                cancellationToken).ConfigureAwait(false);
            return new SendResult {
                Succeeded = true,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                Queued = true,
                QueueMessageId = queuedRecord.MessageId,
                Message = "Message queued successfully."
            };
        }

        try {
            var providerResult = await _sendAsync(session, profile, request, message, cancellationToken).ConfigureAwait(false);
            return new SendResult {
                Succeeded = true,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                ProviderMessageId = providerResult.Id,
                QueueMessageId = message.MessageId,
                Message = "Message sent successfully."
            };
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) when (request.QueueOnFailure && _pendingMessageRepository != null) {
            var queued = await QueueMessageAsync(profile, session, message, null, cancellationToken).ConfigureAwait(false);
            return new SendResult {
                Succeeded = true,
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                Queued = true,
                QueueMessageId = queued.MessageId,
                Message = $"Message queued after send failure: {ex.Message}"
            };
        }
    }

    private static async Task<GraphMessage> DefaultSendAsync(
        GraphSession session,
        MailProfile profile,
        SendMessageRequest request,
        MimeMessage message,
        CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return await GraphMimeMessageSender.SendAsync(session.Client, session.UserId, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PendingMessageRecord> QueueMessageAsync(
        MailProfile profile,
        GraphSession session,
        MimeMessage message,
        DateTimeOffset? notBefore,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(message.MessageId)) {
            message.MessageId = MimeUtils.GenerateMessageId();
        }

        using var stream = new MemoryStream();
        await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var record = new PendingMessageRecord {
            MessageId = message.MessageId!,
            MimeMessage = Convert.ToBase64String(stream.ToArray()),
            Timestamp = now,
            NextAttemptAt = notBefore?.ToUniversalTime() ?? now,
            Provider = EmailProvider.Graph
        };

        record.ProviderData[GraphPendingMessageSender.UserIdKey] = session.UserId;
        var credential = session.Credential;
        var userName = !string.IsNullOrWhiteSpace(credential?.UserName)
            ? credential!.UserName
            : session.UserId;
        record.ProviderData[GraphPendingMessageSender.UserNameKey] = userName;

        if (!string.IsNullOrWhiteSpace(credential?.AccessToken)) {
            record.ProviderData[GraphPendingMessageSender.AccessTokenProtectedKey] =
                CredentialProtection.Default.Protect(credential!.AccessToken);
        }

        if (credential != null) {
            record.ProviderData[GraphPendingMessageSender.ExpiresOnKey] =
                (credential.ExpiresOn == default ? DateTimeOffset.MaxValue : credential.ExpiresOn).ToString("o", CultureInfo.InvariantCulture);
        }

        var graphCredential = session.GraphCredential;
        if (!string.IsNullOrWhiteSpace(graphCredential?.ClientId)) {
            record.ProviderData[GraphPendingMessageSender.ClientIdKey] = graphCredential!.ClientId;
        }
        if (!string.IsNullOrWhiteSpace(graphCredential?.DirectoryId)) {
            record.ProviderData[GraphPendingMessageSender.TenantIdKey] = graphCredential!.DirectoryId;
        }
        if (!string.IsNullOrWhiteSpace(graphCredential?.ClientSecret)) {
            record.ProviderData[GraphPendingMessageSender.ClientSecretProtectedKey] =
                CredentialProtection.Default.Protect(graphCredential!.ClientSecret!);
        }
        if (!string.IsNullOrWhiteSpace(graphCredential?.CertificatePath)) {
            record.ProviderData[GraphPendingMessageSender.CertificatePathKey] = graphCredential!.CertificatePath!;
        }
        if (!string.IsNullOrWhiteSpace(graphCredential?.CertificatePassword)) {
            record.ProviderData[GraphPendingMessageSender.CertificatePasswordProtectedKey] =
                CredentialProtection.Default.Protect(graphCredential!.CertificatePassword!);
        }

        profile.Settings.TryGetValue(MailProfileSettingsKeys.TenantId, out var tenantId);
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !record.ProviderData.ContainsKey(GraphPendingMessageSender.TenantIdKey)) {
            record.ProviderData[GraphPendingMessageSender.TenantIdKey] = tenantId.Trim();
        }

        await _pendingMessageRepository!.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        return record;
    }
}
