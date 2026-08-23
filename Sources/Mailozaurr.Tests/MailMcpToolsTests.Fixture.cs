#if NET8_0_OR_GREATER
using Mailozaurr;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    private sealed class TestFixture : IDisposable {
        private readonly string _tempDirectory;

        public TestFixture() {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "Mailozaurr.McpTests", Guid.NewGuid().ToString("N"));

            ProfileStore = new InMemoryProfileStore(new[] {
                new MailProfile {
                    Id = "gmail-work",
                    DisplayName = "Work Gmail",
                    Kind = MailProfileKind.Gmail
                }
            });
            SecretStore = new InMemorySecretStore();
            ReadService = new FakeReadService();
            EmlExportService = new FakeEmlExportService();
            ChangeFeedService = new FakeChangeFeedService();
            SendService = new FakeSendService();
            MessageActionService = new FakeMessageActionService();
            PlanExchangeService = new FakeMessageActionPlanExchangeService();
            PlanRegistryService = new FakeMessageActionPlanRegistryService();
            QueueService = new FakeQueueService();
            ProfileAuthService = new FakeProfileAuthService(ProfileStore, SecretStore);
            ProfileConnectionService = new FakeProfileConnectionService();

            Application = new MailApplicationBuilder()
                .UseProfileStore(ProfileStore)
                .UseSecretStore(SecretStore)
                .UseDraftStore(new FileMailDraftStore(Path.Combine(_tempDirectory, "drafts.json")))
                .UseProfileAuthService(ProfileAuthService)
                .UseProfileConnectionService(ProfileConnectionService)
                .UseReadService(ReadService)
                .UseEmlExportService(EmlExportService)
                .UseChangeFeedService(ChangeFeedService)
                .UseMessageActionService(MessageActionService)
                .UseMessageActionPlanExchangeService(PlanExchangeService)
                .UseMessageActionPlanRegistryService(PlanRegistryService)
                .UseSendService(SendService)
                .UseQueueService(QueueService)
                .Build();

            Tools = new MailMcpTools(Application);
        }

        public MailApplication Application { get; }

        public MailMcpTools Tools { get; }

        public InMemoryProfileStore ProfileStore { get; }

        public InMemorySecretStore SecretStore { get; }

        public FakeReadService ReadService { get; }

        public FakeEmlExportService EmlExportService { get; }

        public FakeChangeFeedService ChangeFeedService { get; }

        public FakeSendService SendService { get; }

        public FakeMessageActionService MessageActionService { get; }

        public FakeMessageActionPlanExchangeService PlanExchangeService { get; }

        public FakeMessageActionPlanRegistryService PlanRegistryService { get; }

        public FakeQueueService QueueService { get; }

        public FakeProfileAuthService ProfileAuthService { get; }

        public FakeProfileConnectionService ProfileConnectionService { get; }

        public string CreatePath(string fileName) => Path.Combine(_tempDirectory, fileName);

        public void Dispose() {
            if (Directory.Exists(_tempDirectory)) {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }

    private sealed class FakeEmlExportService : IMailEmlExportService {
        public MailEmlExportRequest? LastRequest { get; private set; }

        public Task<MailEmlExportResult> ExportAsync(
            MailEmlExportRequest request,
            CancellationToken cancellationToken = default) {
            LastRequest = request;
            return Task.FromResult(new MailEmlExportResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                DestinationDirectory = request.DestinationDirectory,
                RequestedCount = request.MessageIds.Count,
                ExportedCount = request.MessageIds.Count,
                Message = $"Exported {request.MessageIds.Count} EML message(s)."
            });
        }
    }

    private sealed class FakeChangeFeedService : IMailChangeFeedService {
        public MailChangeFeedRequest? LastFeedRequest { get; private set; }

        public MailChangeSubscriptionRequest? LastSubscriptionRequest { get; private set; }

        public Task<MailChangeFeedResult> GetChangesAsync(MailChangeFeedRequest request, CancellationToken cancellationToken = default) {
            LastFeedRequest = request;
            return Task.FromResult(new MailChangeFeedResult {
                ProfileId = request.ProfileId,
                Provider = MailProfileKind.Gmail,
                NextCursor = "next",
                CursorKind = "durable-history"
            });
        }

        public Task<MailChangeFeedResult> WaitForChangesAsync(MailChangeWaitRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailChangeFeedResult {
                ProfileId = request.ProfileId,
                Provider = MailProfileKind.Imap,
                CursorKind = "ephemeral-idle"
            });

        public Task<MailChangeSubscriptionResult> SubscribeAsync(MailChangeSubscriptionRequest request, CancellationToken cancellationToken = default) {
            LastSubscriptionRequest = request;
            return Task.FromResult(new MailChangeSubscriptionResult {
                ProfileId = request.ProfileId,
                Provider = MailProfileKind.Gmail,
                Succeeded = true,
                Cursor = "watch"
            });
        }

        public Task<MailChangeSubscriptionResult> UnsubscribeAsync(MailChangeUnsubscribeRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MailChangeSubscriptionResult {
                ProfileId = request.ProfileId,
                Provider = MailProfileKind.Gmail,
                Succeeded = true
            });
    }
}
#endif
