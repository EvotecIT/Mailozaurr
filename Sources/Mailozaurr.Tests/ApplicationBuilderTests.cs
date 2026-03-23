using MailKit.Net.Imap;
using Mailozaurr.Application;

namespace Mailozaurr.Tests;

public sealed class ApplicationBuilderTests {
    [Fact]
    public void BuildCreatesDefaultApplicationWithBuiltInReadAndSendHandlers() {
        var profileDirectory = CreateTemporaryDirectory();
        var secretDirectory = CreateTemporaryDirectory();
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = profileDirectory },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = secretDirectory }
        });

        var app = builder.Build();

        Assert.NotNull(app.ProfileStore);
        Assert.NotNull(app.SecretStore);
        Assert.NotNull(app.DraftStore);
        Assert.NotNull(app.Profiles);
        Assert.NotNull(app.ProfileOverview);
        Assert.NotNull(app.ProfileConnections);
        Assert.NotNull(app.Drafts);
        Assert.NotNull(app.DraftExchange);
        Assert.NotNull(app.ProfileBootstrap);
        Assert.NotNull(app.ProfileAuth);
        Assert.NotNull(app.FolderAliases);
        Assert.NotNull(app.Read);
        Assert.NotNull(app.MessageActionPreview);
        Assert.NotNull(app.MessageActionPlans);
        Assert.NotNull(app.MessageActionPlanExchange);
        Assert.NotNull(app.MessageActionPlanRegistry);
        Assert.NotNull(app.MessageActionBatch);
        Assert.NotNull(app.MessageActions);
        Assert.NotNull(app.Send);
        Assert.NotNull(app.Queue);
        Assert.Contains(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Imap);
        Assert.Contains(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.Contains(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Gmail);
        Assert.Contains(app.MessageActionHandlers, handler => handler.Kind == MailProfileKind.Imap);
        Assert.Contains(app.MessageActionHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.Contains(app.MessageActionHandlers, handler => handler.Kind == MailProfileKind.Gmail);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Gmail);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Smtp);
    }

    [Fact]
    public void BuildHonorsDisabledImapHandlerOption() {
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            EnableImapReadHandler = false,
            EnableGraphReadHandler = false,
            EnableGraphSendHandler = false,
            EnableGmailReadHandler = false,
            EnableGmailSendHandler = false,
            EnableSmtpSendHandler = false,
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = CreateTemporaryDirectory() },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = CreateTemporaryDirectory() }
        });

        var app = builder.Build();

        Assert.DoesNotContain(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Imap);
        Assert.DoesNotContain(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.DoesNotContain(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Gmail);
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.Gmail);
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.Smtp);
    }

    [Fact]
    public void BuildUsesInjectedHandlersAndFactories() {
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            EnableImapReadHandler = false,
            EnableGraphReadHandler = false,
            EnableGraphSendHandler = false,
            EnableGmailReadHandler = false,
            EnableGmailSendHandler = false,
            EnableSmtpSendHandler = false,
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = CreateTemporaryDirectory() },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = CreateTemporaryDirectory() }
        });
        builder.UseImapSessionFactory(new FakeImapSessionFactory());
        builder.UseGraphSessionFactory(new FakeGraphSessionFactory());
        builder.UseGmailSessionFactory(new FakeGmailSessionFactory());
        builder.UseSmtpSessionFactory(new FakeSmtpSessionFactory());
        builder.AddReadHandler(new FakeReadHandler(MailProfileKind.Graph));
        builder.AddMessageActionHandler(new FakeMessageActionHandler(MailProfileKind.Graph));
        builder.AddSendHandler(new FakeSendHandler(MailProfileKind.Graph));

        var app = builder.Build();

        Assert.Contains(app.ReadHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.Contains(app.MessageActionHandlers, handler => handler.Kind == MailProfileKind.Graph);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Graph);
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeImapSessionFactory : IImapSessionFactory {
        public Task<ImapClient> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImapClient());
    }

    private sealed class FakeGraphSessionFactory : IGraphSessionFactory {
        public Task<GraphSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GraphSession(new GraphApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }

    private sealed class FakeGmailSessionFactory : IGmailSessionFactory {
        public Task<GmailSession> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GmailSession(new GmailApiClient(new OAuthCredential {
                UserName = profile.DefaultMailbox ?? "me",
                AccessToken = "token",
                ExpiresOn = DateTimeOffset.MaxValue
            }), profile.DefaultMailbox ?? "me"));
    }

    private sealed class FakeSmtpSessionFactory : ISmtpSessionFactory {
        public Task<Smtp> ConnectAsync(MailProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Smtp());
    }

    private sealed class FakeReadHandler : IMailReadHandler {
        public FakeReadHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailProfile profile, MailFolderQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FolderRef>>(Array.Empty<FolderRef>());

        public Task<MessageDetail?> GetMessageAsync(MailProfile profile, GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(null);

        public Task<OperationResult> SaveAttachmentAsync(MailProfile profile, SaveAttachmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success());

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailProfile profile, MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummary>>(Array.Empty<MessageSummary>());
    }

    private sealed class FakeSendHandler : IMailSendHandler {
        public FakeSendHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public Task<SendResult> SendAsync(MailProfile profile, SendMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SendResult { Succeeded = true, ProfileKind = Kind });
    }

    private sealed class FakeMessageActionHandler : IMailMessageActionHandler {
        public FakeMessageActionHandler(MailProfileKind kind) {
            Kind = kind;
        }

        public MailProfileKind Kind { get; }

        public Task<MessageActionResult> SetReadStateAsync(MailProfile profile, SetReadStateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult { Succeeded = true, ProfileId = profile.Id });

        public Task<MessageActionResult> SetFlaggedStateAsync(MailProfile profile, SetFlaggedStateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult { Succeeded = true, ProfileId = profile.Id });

        public Task<MessageActionResult> MoveAsync(MailProfile profile, MoveMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult { Succeeded = true, ProfileId = profile.Id });

        public Task<MessageActionResult> DeleteAsync(MailProfile profile, DeleteMessagesRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MessageActionResult { Succeeded = true, ProfileId = profile.Id });
    }
}
