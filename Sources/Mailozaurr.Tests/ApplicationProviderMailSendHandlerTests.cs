using Mailozaurr;
using Mailozaurr.Definitions;
using MimeKit;
using System.Net;
using System.Runtime.CompilerServices;

namespace Mailozaurr.Tests;

public sealed class ApplicationProviderMailSendHandlerTests : IDisposable {
    private readonly List<string> _temporaryFiles = new();

    [Fact]
    public async Task SendGridHandlerMapsProfileMessageAndSecret() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var credential = Assert.IsType<NetworkCredential>(client.Credentials);
            Assert.Equal("sg-secret", credential.Password);
            var sender = Assert.IsType<SendGridEmailAddress>(client.From);
            Assert.Equal("Sender", sender.Name);
            Assert.Equal("sender@example.com", sender.Email);
            var recipient = Assert.IsType<SendGridEmailAddress>(Assert.Single(client.To!));
            Assert.Equal("Alice", recipient.Name);
            Assert.Equal("alice@example.com", recipient.Email);
            Assert.Equal("Provider test", client.Subject);
            Assert.Equal(MessagePriority.High, client.Priority);
            Assert.Equal(2, client.RetryCount);
            AssertAttachmentMetadata(Assert.Single(client.Attachments!));
            return Task.FromResult(Succeeded("sendgrid-message"));
        });

        var result = await handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), CreateRequest("sendgrid"));

        Assert.True(result.Succeeded);
        Assert.Equal("sendgrid-message", result.ProviderMessageId);
    }

    [Fact]
    public async Task MailgunHandlerMapsDomainAndApiKey() {
        var secrets = new FakeSecretStore(("mailgun", MailSecretNames.ApiKey, "mg-secret"));
        var profile = CreateProfile("mailgun", MailProfileKind.Mailgun);
        profile.Settings[MailProfileSettingsKeys.Domain] = "mg.example.com";
        var handler = new MailgunMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var credential = Assert.IsType<NetworkCredential>(client.Credentials);
            Assert.Equal("mg-secret", credential.Password);
            Assert.Equal("mg.example.com", client.Domain);
            var recipient = Assert.IsType<MailboxAddress>(Assert.Single(client.To));
            Assert.Equal("Alice", recipient.Name);
            Assert.Equal("alice@example.com", recipient.Address);
            Assert.Equal(MessagePriority.High, client.Priority);
            AssertAttachmentMetadata(Assert.Single(client.Attachments!));
            return Task.FromResult(Succeeded("mailgun-message"));
        });

        var result = await handler.SendAsync(profile, CreateRequest("mailgun"));

        Assert.True(result.Succeeded);
        Assert.Equal("mailgun-message", result.ProviderMessageId);
    }

    [Fact]
    public async Task SesHandlerMapsRegionAndCredentialPair() {
        var secrets = new FakeSecretStore(
            ("ses", MailSecretNames.AccessKeyId, "access-key"),
            ("ses", MailSecretNames.SecretAccessKey, "secret-key"));
        var profile = CreateProfile("ses", MailProfileKind.Ses);
        profile.Settings[MailProfileSettingsKeys.Region] = "eu-central-1";
        var handler = new SesMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var credential = Assert.IsType<NetworkCredential>(client.Credentials);
            Assert.Equal("access-key", credential.UserName);
            Assert.Equal("secret-key", credential.Password);
            Assert.Equal("eu-central-1", client.Region);
            var recipient = Assert.IsType<MailboxAddress>(Assert.Single(client.To));
            Assert.Equal("Alice", recipient.Name);
            Assert.Equal("alice@example.com", recipient.Address);
            Assert.Equal(MessagePriority.High, client.Priority);
            AssertAttachmentMetadata(Assert.Single(client.Attachments!));
            return Task.FromResult(Succeeded("ses-message"));
        });

        var result = await handler.SendAsync(profile, CreateRequest("ses"));

        Assert.True(result.Succeeded);
        Assert.Equal("ses-message", result.ProviderMessageId);
    }

    [Fact]
    public async Task ProviderHandlerPreservesConfirmedQueueAfterRecordIsConsumed() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var pending = new FakePendingMessageRepository();
        var handler = new SendGridMailSendHandler(secrets, pending, sendAsync: (client, cancellationToken) =>
            Task.FromResult(new SmtpResult(false, EmailAction.Send, "alice@example.com", "sender@example.com", "SendGridApi", 0, TimeSpan.Zero, error: "temporary failure") {
                MessageId = "queued-message",
                Queued = true
            }));

        var request = CreateRequest("sendgrid");
        request.QueueOnFailure = true;
        var result = await handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request);

        Assert.True(result.Succeeded);
        Assert.True(result.Queued);
        Assert.Equal("queued-message", result.QueueMessageId);
    }

    [Fact]
    public async Task ProviderHandlerDoesNotClaimQueueSuccessWithoutPersistedRecord() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var pending = new FakePendingMessageRepository();
        var handler = new SendGridMailSendHandler(secrets, pending, sendAsync: (client, cancellationToken) =>
            Task.FromResult(new SmtpResult(false, EmailAction.Send, "alice@example.com", "sender@example.com", "SendGridApi", 0, TimeSpan.Zero, error: "temporary failure") {
                MessageId = "not-persisted",
                Queued = false
            }));

        var request = CreateRequest("sendgrid");
        request.QueueOnFailure = true;
        var result = await handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request);

        Assert.False(result.Succeeded);
        Assert.False(result.Queued);
        Assert.Null(result.QueueMessageId);
    }

    [Fact]
    public async Task ProviderHandlerRejectsMessagesWithoutAUsableRecipient() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) =>
            throw new InvalidOperationException("Provider dispatch must not be reached."));
        var request = CreateRequest("sendgrid");
        request.Message.To.Clear();
        request.Message.Cc.Add(new MessageRecipient { Address = "  " });
        request.Message.Bcc.Add(new MessageRecipient { Address = string.Empty });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request));

        Assert.Contains("recipient", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProviderHandlerRejectsAMissingAttachmentBeforeDispatch() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var request = CreateRequest("sendgrid");
        File.Delete(request.Message.Attachments[0].Path);
        var dispatched = false;
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            dispatched = true;
            return Task.FromResult(Succeeded("sendgrid-message"));
        });

        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request));

        Assert.False(dispatched);
        Assert.Equal(Path.GetFullPath(request.Message.Attachments[0].Path), exception.FileName);
    }

    [Fact]
    public async Task ProviderHandlersUseTheFirstNonBlankReplyToAddress() {
        var sendGridSecrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var sendGrid = new SendGridMailSendHandler(sendGridSecrets, sendAsync: (client, cancellationToken) => {
            var replyTo = Assert.IsType<SendGridEmailAddress>(client.ReplyTo);
            Assert.Equal("reply@example.com", replyTo.Email);
            return Task.FromResult(Succeeded("sendgrid-message"));
        });
        await sendGrid.SendAsync(
            CreateProfile("sendgrid", MailProfileKind.SendGrid),
            CreateRequestWithReplyTo("sendgrid"));

        var mailgunSecrets = new FakeSecretStore(("mailgun", MailSecretNames.ApiKey, "mg-secret"));
        var mailgun = new MailgunMailSendHandler(mailgunSecrets, sendAsync: (client, cancellationToken) => {
            var replyTo = Assert.IsType<MailboxAddress>(client.ReplyTo);
            Assert.Equal("reply@example.com", replyTo.Address);
            return Task.FromResult(Succeeded("mailgun-message"));
        });
        await mailgun.SendAsync(
            CreateProfile("mailgun", MailProfileKind.Mailgun),
            CreateRequestWithReplyTo("mailgun"));

        var sesSecrets = new FakeSecretStore(
            ("ses", MailSecretNames.AccessKeyId, "access-key"),
            ("ses", MailSecretNames.SecretAccessKey, "secret-key"));
        var ses = new SesMailSendHandler(sesSecrets, sendAsync: (client, cancellationToken) => {
            var replyTo = Assert.IsType<MailboxAddress>(client.ReplyTo);
            Assert.Equal("reply@example.com", replyTo.Address);
            return Task.FromResult(Succeeded("ses-message"));
        });
        await ses.SendAsync(
            CreateProfile("ses", MailProfileKind.Ses),
            CreateRequestWithReplyTo("ses"));
    }

    [Fact]
    public async Task ProviderAttachmentKeepsThePathDerivedFileNameWhenNoOverrideIsProvided() {
        var secrets = new FakeSecretStore(
            ("ses", MailSecretNames.AccessKeyId, "access-key"),
            ("ses", MailSecretNames.SecretAccessKey, "secret-key"));
        var request = CreateRequest("ses");
        request.Message.Attachments[0].FileName = null;
        request.Message.Attachments[0].ContentType = null;
        var expectedFileName = Path.GetFileName(request.Message.Attachments[0].Path);
        var handler = new SesMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var attachment = Assert.IsType<FileAttachmentDescriptor>(Assert.Single(client.Attachments!));
            Assert.Equal(expectedFileName, attachment.FileName);
            return Task.FromResult(Succeeded("ses-message"));
        });

        await handler.SendAsync(CreateProfile("ses", MailProfileKind.Ses), request);
    }

    [Fact]
    public async Task ProviderHandlerFallsBackToProfileSenderWhenDraftSenderIsWhitespace() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var request = CreateRequest("sendgrid");
        request.Message.From = new MessageRecipient { Address = "  " };
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var sender = Assert.IsType<SendGridEmailAddress>(client.From);
            Assert.Equal("sender@example.com", sender.Email);
            return Task.FromResult(Succeeded("sendgrid-message"));
        });

        await handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request);
    }

    [Fact]
    public async Task ProviderHandlerFallsBackToDefaultMailboxWhenDefaultSenderIsMissing() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var request = CreateRequest("sendgrid");
        request.Message.From = null;
        var profile = CreateProfile("sendgrid", MailProfileKind.SendGrid);
        profile.DefaultSender = null;
        profile.DefaultMailbox = "Mailbox Sender <mailbox@example.com>";
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var sender = Assert.IsType<SendGridEmailAddress>(client.From);
            Assert.Equal("Mailbox Sender", sender.Name);
            Assert.Equal("mailbox@example.com", sender.Email);
            return Task.FromResult(Succeeded("sendgrid-message"));
        });

        await handler.SendAsync(profile, request);
    }

    [Fact]
    public async Task ProviderInlineAttachmentGetsAUsableContentIdWhenNoOverrideIsProvided() {
        var secrets = new FakeSecretStore(("sendgrid", MailSecretNames.ApiKey, "sg-secret"));
        var request = CreateRequest("sendgrid");
        request.Message.Attachments[0].IsInline = true;
        request.Message.Attachments[0].ContentId = null;
        var handler = new SendGridMailSendHandler(secrets, sendAsync: (client, cancellationToken) => {
            var attachment = Assert.Single(client.Attachments!);
            Assert.Equal("renamed-report.pdf", attachment.ContentId);
            Assert.Equal(ContentDisposition.Inline, attachment.ContentDisposition?.Disposition);
            return Task.FromResult(Succeeded("sendgrid-message"));
        });

        await handler.SendAsync(CreateProfile("sendgrid", MailProfileKind.SendGrid), request);
    }

    private static MailProfile CreateProfile(string id, MailProfileKind kind) => new() {
        Id = id,
        DisplayName = id,
        Kind = kind,
        DefaultSender = "Sender <sender@example.com>",
        Settings = new Dictionary<string, string> {
            [MailProfileSettingsKeys.RetryCount] = "2"
        }
    };

    private SendMessageRequest CreateRequest(string profileId) {
        var path = Path.GetTempFileName();
        _temporaryFiles.Add(path);
        return new SendMessageRequest {
            ProfileId = profileId,
            Message = new DraftMessage {
                ProfileId = profileId,
                Subject = "Provider test",
                TextBody = "Hello",
                Priority = MessagePriority.High,
                To = {
                    new MessageRecipient { Name = "Alice", Address = "alice@example.com" }
                },
                Attachments = {
                    new DraftAttachment {
                        Path = path,
                        FileName = "renamed-report.pdf",
                        ContentType = "application/pdf",
                        ContentId = "report-content"
                    }
                }
            }
        };
    }

    private SendMessageRequest CreateRequestWithReplyTo(string profileId) {
        var request = CreateRequest(profileId);
        request.Message.ReplyTo.Add(new MessageRecipient { Address = "  " });
        request.Message.ReplyTo.Add(new MessageRecipient { Name = "Reply", Address = "reply@example.com" });
        return request;
    }

    private static void AssertAttachmentMetadata(AttachmentDescriptor attachment) {
        Assert.Equal("renamed-report.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal("report-content", attachment.ContentId);
        Assert.Equal(ContentDisposition.Attachment, attachment.ContentDisposition?.Disposition);
    }

    private static SmtpResult Succeeded(string messageId) =>
        new(true, EmailAction.Send, "alice@example.com", "sender@example.com", "provider", 0, TimeSpan.Zero) {
            MessageId = messageId
        };

    public void Dispose() {
        foreach (var path in _temporaryFiles) {
            try {
                File.Delete(path);
            } catch (IOException) {
            }
        }
    }

    private sealed class FakeSecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _secrets;

        public FakeSecretStore(params (string ProfileId, string Name, string Value)[] secrets) {
            _secrets = secrets.ToDictionary(
                secret => $"{secret.ProfileId}:{secret.Name}",
                secret => secret.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue($"{profileId}:{secretName}", out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[$"{profileId}:{secretName}"] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove($"{profileId}:{secretName}"));
    }

    private sealed class FakePendingMessageRepository : IPendingMessageRepository {
        private readonly Dictionary<string, PendingMessageRecord> _records;

        public FakePendingMessageRepository(params PendingMessageRecord[] records) {
            _records = records.ToDictionary(record => record.MessageId, StringComparer.OrdinalIgnoreCase);
        }

        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) {
            _records[record.MessageId] = record;
            return Task.CompletedTask;
        }

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) =>
            GetByMessageIdAsync(messageId, cancellationToken);

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) {
            _records.TryGetValue(messageId, out var record);
            return Task.FromResult<PendingMessageRecord?>(record);
        }

        public async IAsyncEnumerable<PendingMessageRecord> GetAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var record in _records.Values) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return record;
                await Task.Yield();
            }
        }

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            _records.Remove(messageId);
            return Task.CompletedTask;
        }
    }
}
