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
}
#endif