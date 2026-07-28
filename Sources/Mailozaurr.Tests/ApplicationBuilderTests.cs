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
        Assert.NotNull(app.ProfileSecrets);
        Assert.NotNull(app.ProfileSecretMaintenance);
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
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.SendGrid);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Mailgun);
        Assert.Contains(app.SendHandlers, handler => handler.Kind == MailProfileKind.Ses);
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
            EnableSendGridSendHandler = false,
            EnableMailgunSendHandler = false,
            EnableSesSendHandler = false,
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
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.SendGrid);
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.Mailgun);
        Assert.DoesNotContain(app.SendHandlers, handler => handler.Kind == MailProfileKind.Ses);
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
            EnableSendGridSendHandler = false,
            EnableMailgunSendHandler = false,
            EnableSesSendHandler = false,
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

    [Fact]
    public async Task BuildUsesRegisteredCustomHandlerCapabilitiesWithoutStaticDefaults() {
        var directory = CreateTemporaryDirectory();
        try {
            var store = new FileMailProfileStore(Path.Combine(directory, "profiles.json"));
            await store.SaveAsync(new MailProfile {
                Id = "custom-provider",
                DisplayName = "Custom provider",
                Kind = MailProfileKind.SendGrid
            });
            var builder = CreateCustomHandlerBuilder(directory, store);
            builder.AddReadHandler(new FakeReadHandler(MailProfileKind.SendGrid));
            builder.AddMessageActionHandler(new FakeMessageActionHandler(MailProfileKind.SendGrid));
            builder.AddSendHandler(new FakeSendHandler(MailProfileKind.SendGrid));
            var app = builder.Build();

            ProfileCapabilities? capabilities = await app.Profiles.GetCapabilitiesAsync("custom-provider");
            var search = await app.Read.SearchAsync(new MailSearchRequest { ProfileId = "custom-provider" });
            var preview = await app.MessageActionPreview.PreviewReadStateAsync(new SetReadStateRequest {
                ProfileId = "custom-provider",
                MessageIds = { "message-1" },
                IsRead = true
            });
            var action = await app.MessageActions.SetReadStateAsync(new SetReadStateRequest {
                ProfileId = "custom-provider",
                MessageIds = { "message-1" },
                IsRead = true,
                ConfirmationToken = preview.ConfirmationToken
            });
            var send = await app.Send.SendAsync(new SendMessageRequest {
                ProfileId = "custom-provider",
                Message = new DraftMessage { ProfileId = "custom-provider", Subject = "Hello" }
            });

            Assert.NotNull(capabilities);
            Assert.True(capabilities!.Supports(MailCapability.SearchMessages));
            Assert.True(capabilities.Supports(MailCapability.MarkMessages));
            Assert.True(capabilities.Supports(MailCapability.SendMessages));
            Assert.Empty(search);
            Assert.True(preview.Succeeded);
            Assert.True(action.Succeeded);
            Assert.True(send.Succeeded);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task BuildHonorsExplicitCapabilityOverrideForCustomHandler() {
        var directory = CreateTemporaryDirectory();
        try {
            var store = new FileMailProfileStore(Path.Combine(directory, "profiles.json"));
            await store.SaveAsync(new MailProfile {
                Id = "disabled-custom-provider",
                DisplayName = "Disabled custom provider",
                Kind = MailProfileKind.SendGrid,
                Capabilities = new ProfileCapabilities(MailProfileKind.SendGrid, MailCapability.None)
            });
            var builder = CreateCustomHandlerBuilder(directory, store);
            builder.AddSendHandler(new FakeSendHandler(MailProfileKind.SendGrid));
            var app = builder.Build();

            ProfileCapabilities? capabilities = await app.Profiles.GetCapabilitiesAsync("disabled-custom-provider");
            Assert.NotNull(capabilities);
            Assert.False(capabilities!.Supports(MailCapability.SendMessages));
            await Assert.ThrowsAsync<NotSupportedException>(() => app.Send.SendAsync(new SendMessageRequest {
                ProfileId = "disabled-custom-provider",
                Message = new DraftMessage { ProfileId = "disabled-custom-provider", Subject = "Hello" }
            }));
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task BuildReportsProviderCapabilitiesBackedByInjectedServicesWhenHandlersAreDisabled() {
        var directory = CreateTemporaryDirectory();
        try {
            var store = new FileMailProfileStore(Path.Combine(directory, "profiles.json"));
            var profiles = new[] {
                new MailProfile {
                    Id = "pop3-service-override",
                    DisplayName = "POP3 service override",
                    Kind = MailProfileKind.Pop3
                },
                new MailProfile {
                    Id = "sendgrid-service-override",
                    DisplayName = "SendGrid service override",
                    Kind = MailProfileKind.SendGrid
                },
                new MailProfile {
                    Id = "mailgun-service-override",
                    DisplayName = "Mailgun service override",
                    Kind = MailProfileKind.Mailgun
                },
                new MailProfile {
                    Id = "ses-service-override",
                    DisplayName = "SES service override",
                    Kind = MailProfileKind.Ses
                }
            };
            foreach (MailProfile profile in profiles) {
                await store.SaveAsync(profile);
            }
            var builder = CreateCustomHandlerBuilder(directory, store)
                .UseReadService(new RoutedMailReadService(
                    store,
                    new[] { new FakeReadHandler(MailProfileKind.Pop3) }))
                .UseSendService(new RoutedMailSendService(
                    store,
                    new[] {
                        new FakeSendHandler(MailProfileKind.SendGrid),
                        new FakeSendHandler(MailProfileKind.Mailgun),
                        new FakeSendHandler(MailProfileKind.Ses)
                    }));

            MailApplication app = builder.Build();
            ProfileCapabilities? pop3Capabilities =
                await app.Profiles.GetCapabilitiesAsync("pop3-service-override");
            IReadOnlyList<MessageSummary> search = await app.Read.SearchAsync(new MailSearchRequest {
                ProfileId = "pop3-service-override"
            });

            Assert.NotNull(pop3Capabilities);
            Assert.True(pop3Capabilities!.Supports(MailCapability.SearchMessages));
            Assert.False(pop3Capabilities.Supports(MailCapability.SendMessages));
            Assert.Empty(search);

            foreach (MailProfile profile in profiles.Where(profile =>
                         profile.Kind is MailProfileKind.SendGrid or MailProfileKind.Mailgun or MailProfileKind.Ses)) {
                ProfileCapabilities? capabilities = await app.Profiles.GetCapabilitiesAsync(profile.Id);
                SendResult send = await app.Send.SendAsync(new SendMessageRequest {
                    ProfileId = profile.Id,
                    Message = new DraftMessage {
                        ProfileId = profile.Id,
                        Subject = "Hello"
                    }
                });

                Assert.NotNull(capabilities);
                Assert.True(capabilities!.Supports(MailCapability.SendMessages));
                Assert.False(capabilities.Supports(MailCapability.SearchMessages));
                Assert.True(send.Succeeded);
            }
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BuildRejectsCustomPendingRepositoryWithoutMatchingDeadLetterRepository() {
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            EnableImapReadHandler = false,
            EnableGraphReadHandler = false,
            EnableGraphSendHandler = false,
            EnableGmailReadHandler = false,
            EnableGmailSendHandler = false,
            EnableSmtpSendHandler = false,
            EnableSendGridSendHandler = false,
            EnableMailgunSendHandler = false,
            EnableSesSendHandler = false,
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = CreateTemporaryDirectory() },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = CreateTemporaryDirectory() }
        });
        builder.UsePendingMessageRepository(new FakePendingMessageRepository());

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains(nameof(MailApplicationBuilder.UsePendingMessageDeadLetterRepository), exception.Message);
    }

    [Fact]
    public void BuildUsesCustomPendingRepositoriesTogether() {
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            EnableImapReadHandler = false,
            EnableGraphReadHandler = false,
            EnableGraphSendHandler = false,
            EnableGmailReadHandler = false,
            EnableGmailSendHandler = false,
            EnableSmtpSendHandler = false,
            EnableSendGridSendHandler = false,
            EnableMailgunSendHandler = false,
            EnableSesSendHandler = false,
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = CreateTemporaryDirectory() },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = CreateTemporaryDirectory() }
        });
        builder.UsePendingMessageRepository(new FakePendingMessageRepository());
        builder.UsePendingMessageDeadLetterRepository(new FakePendingMessageDeadLetterRepository());

        var app = builder.Build();

        Assert.NotNull(app.Queue);
    }

    [Fact]
    public void BuildUsesInjectedProfileSecretMaintenanceService() {
        var maintenanceService = new FakeProfileSecretMaintenanceService();
        var builder = new MailApplicationBuilder(new MailApplicationOptions {
            ProfileStore = new MailProfileStoreOptions { DirectoryPath = CreateTemporaryDirectory() },
            SecretStore = new MailSecretStoreOptions { DirectoryPath = CreateTemporaryDirectory() }
        }).UseProfileSecretMaintenanceService(maintenanceService);

        MailApplication app = builder.Build();

        Assert.Same(maintenanceService, app.ProfileSecretMaintenance);
    }

    private static string CreateTemporaryDirectory() {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static MailApplicationBuilder CreateCustomHandlerBuilder(string directory, IMailProfileStore store) =>
        new MailApplicationBuilder(new MailApplicationOptions {
            EnableImapReadHandler = false,
            EnableGraphReadHandler = false,
            EnableGraphSendHandler = false,
            EnableGmailReadHandler = false,
            EnableGmailSendHandler = false,
            EnableSmtpSendHandler = false,
            EnableSendGridSendHandler = false,
            EnableMailgunSendHandler = false,
            EnableSesSendHandler = false,
            SecretStore = new MailSecretStoreOptions { DirectoryPath = Path.Combine(directory, "secrets") }
        }).UseProfileStore(store);

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

    private sealed class FakePendingMessageRepository : IPendingMessageRepository {
        public Task SaveAsync(PendingMessageRecord record, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PendingMessageRecord?> TryAcquireLeaseAsync(
            string messageId,
            DateTimeOffset dueBeforeOrAt,
            DateTimeOffset leaseUntil,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PendingMessageRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<PendingMessageRecord> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakePendingMessageDeadLetterRepository : IPendingMessageDeadLetterRepository {
        public Task SaveAsync(PendingMessageDeadLetterRecord record, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PendingMessageDeadLetterRecord?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<PendingMessageDeadLetterRecord> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveAsync(string messageId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeProfileSecretMaintenanceService : IMailProfileSecretMaintenanceService {
        public Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailProfileSecretMaintenanceResult { Succeeded = true });

        public Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailProfileSecretMaintenanceResult { Succeeded = true });
    }
}
