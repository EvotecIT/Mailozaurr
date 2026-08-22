#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    private sealed class FakeQueueService : IMailQueueService {
        public string? LastGetMessageId { get; private set; }

        public string? LastRemoveMessageId { get; private set; }

        public int ProcessCalls { get; private set; }

        public Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageCompact?>(new QueuedMessageCompact {
                MessageId = messageId,
                Provider = "Gmail",
                ProfileKind = MailProfileKind.Gmail,
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                AttemptCount = 0,
                IsDue = false,
                HasProviderData = false,
                Summary = $"{messageId} [Gmail] attempts=0"
            });

        public Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default) {
            LastGetMessageId = messageId;
            return Task.FromResult<QueuedMessageSummary?>(new QueuedMessageSummary {
                MessageId = messageId,
                Provider = "Gmail",
                ProfileKind = MailProfileKind.Gmail,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1)
            });
        }

        public Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageCompact>>(new[] {
                new QueuedMessageCompact {
                    MessageId = "queued-1",
                    Provider = "Gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    AttemptCount = 1,
                    IsDue = false,
                    HasProviderData = true,
                    Summary = "queued-1 [Gmail] attempts=1"
                }
            });

        public Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(new[] {
                new QueuedMessageSummary {
                    MessageId = "queued-1",
                    Provider = "Gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1),
                    AttemptCount = 1,
                    HasProviderData = true
                }
            });

        public Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default) {
            ProcessCalls++;
            return Task.FromResult(new QueueProcessResult {
                Succeeded = true,
                AttemptedCount = 1,
                SentCount = 1,
                Message = "Queue processing completed."
            });
        }

        public Task<IReadOnlyList<QueuedMessageSummary>> ListDeadLettersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(new[] {
                new QueuedMessageSummary {
                    MessageId = "dead-1",
                    Provider = "Gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    QueuedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    AttemptCount = 3,
                    IsDeadLetter = true,
                    DeadLetterReason = "PermanentFailure",
                    ErrorMessage = "Authentication failed."
                }
            });

        public Task<QueuedMessageSummary?> GetDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageSummary?>(new QueuedMessageSummary {
                MessageId = messageId,
                Provider = "Gmail",
                ProfileKind = MailProfileKind.Gmail,
                QueuedAt = DateTimeOffset.UtcNow.AddHours(-1),
                NextAttemptAt = DateTimeOffset.UtcNow,
                AttemptCount = 3,
                IsDeadLetter = true,
                DeadLetterReason = "PermanentFailure",
                ErrorMessage = "Authentication failed."
            });

        public Task<OperationResult> RemoveDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Dead-lettered message removed."));

        public Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default) {
            LastRemoveMessageId = messageId;
            return Task.FromResult(OperationResult.Success("Queued message removed."));
        }
    }

    private sealed class FakeDraftService : IMailDraftService {
        public string? LastRequestedDraftId { get; private set; }

        public MailDraft? LastSavedDraft { get; private set; }

        public Task<MailDraftCompact?> GetDraftCompactAsync(string draftId, CancellationToken cancellationToken = default) {
            LastRequestedDraftId = draftId;
            return Task.FromResult<MailDraftCompact?>(new MailDraftCompact {
                Id = draftId,
                Name = "Saved draft",
                ProfileId = "work-imap",
                Subject = "Saved subject",
                ToCount = 1,
                AttachmentCount = 0,
                UpdatedAt = DateTimeOffset.UtcNow,
                Summary = $"{draftId} [work-imap] Saved draft"
            });
        }

        public Task<MailDraft?> GetDraftAsync(string draftId, CancellationToken cancellationToken = default) {
            LastRequestedDraftId = draftId;
            return Task.FromResult<MailDraft?>(new MailDraft {
                Id = draftId,
                Name = "Saved draft",
                Message = new DraftMessage {
                    ProfileId = "work-imap",
                    Subject = "Saved subject",
                    To = {
                        new MessageRecipient { Address = "saved@example.com" }
                    }
                }
            });
        }

        public Task<IReadOnlyList<MailDraftCompact>> GetDraftsCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailDraftCompact>>(new[] {
                new MailDraftCompact {
                    Id = "draft-1",
                    Name = "Saved draft",
                    ProfileId = "work-imap",
                    Subject = "Saved subject",
                    ToCount = 0,
                    AttachmentCount = 0,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Summary = "draft-1 [work-imap] Saved draft"
                }
            });

        public Task<IReadOnlyList<MailDraft>> GetDraftsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailDraft>>(new[] {
                new MailDraft {
                    Id = "draft-1",
                    Name = "Saved draft",
                    Message = new DraftMessage {
                        ProfileId = "work-imap",
                        Subject = "Saved subject"
                    }
                }
            });

        public Task<OperationResult> SaveAsync(MailDraft draft, CancellationToken cancellationToken = default) {
            LastSavedDraft = draft;
            return Task.FromResult(OperationResult.Success("Draft saved."));
        }

        public Task<OperationResult> DeleteAsync(string draftId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Draft deleted."));
    }

    private sealed class FakeDraftExchangeService : IMailDraftExchangeService {
        public string? LastLoadedPath { get; private set; }

        public string? LastSavedPath { get; private set; }

        public Task<MailDraft> LoadAsync(string path, CancellationToken cancellationToken = default) {
            LastLoadedPath = path;
            return Task.FromResult(new MailDraft {
                Id = "imported-draft",
                Name = "Imported draft",
                Message = new DraftMessage {
                    ProfileId = "work-imap",
                    Subject = "Imported subject",
                    To = {
                        new MessageRecipient { Address = "imported@example.com" }
                    }
                }
            });
        }

        public Task SaveAsync(string path, MailDraft draft, CancellationToken cancellationToken = default) {
            LastSavedPath = path;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileAuthService : IMailProfileAuthService {
        private readonly InMemoryProfileStore _profileStore;
        private readonly InMemorySecretStore _secretStore;

        public FakeProfileAuthService(InMemoryProfileStore profileStore, InMemorySecretStore secretStore) {
            _profileStore = profileStore;
            _secretStore = secretStore;
        }

        public GmailProfileLoginRequest? LastGmailRequest { get; private set; }

        public GraphProfileLoginRequest? LastGraphRequest { get; private set; }

        public string? LastRefreshProfileId { get; private set; }

        public string? LastStatusProfileId { get; private set; }

        public int RefreshCalls { get; private set; }

        public async Task<MailProfileAuthStatus?> GetStatusAsync(string profileId, CancellationToken cancellationToken = default) {
            LastStatusProfileId = profileId;
            var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken);
            if (profile == null) {
                return null;
            }

            var accessToken = await _secretStore.GetSecretAsync(profileId, MailSecretNames.AccessToken, cancellationToken);
            var refreshToken = await _secretStore.GetSecretAsync(profileId, MailSecretNames.RefreshToken, cancellationToken);
            var clientSecret = await _secretStore.GetSecretAsync(profileId, MailSecretNames.ClientSecret, cancellationToken);
            var password = await _secretStore.GetSecretAsync(profileId, MailSecretNames.Password, cancellationToken);

            return new MailProfileAuthStatus {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                AuthFlow = profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) ? authFlow : null,
                Mode = profile.Kind == MailProfileKind.Imap && !string.IsNullOrWhiteSpace(password) ? "basic" : "interactive",
                Mailbox = profile.DefaultMailbox,
                HasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
                HasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
                HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
                HasPassword = !string.IsNullOrWhiteSpace(password),
                CanRefresh = profile.Kind is MailProfileKind.Gmail or MailProfileKind.Graph,
                CanLoginInteractively = profile.Kind is MailProfileKind.Gmail or MailProfileKind.Graph,
                Summary = $"{profile.Id} [{profile.Kind}] auth status available."
            };
        }

        public async Task<MailProfileAuthenticationResult> LoginGmailAsync(GmailProfileLoginRequest request, CancellationToken cancellationToken = default) {
            var profile = await _profileStore.GetByIdAsync(request.ProfileId, cancellationToken);
            Assert.NotNull(profile);
            var savedProfile = profile!;
            var clientSecret = request.ClientSecret;
            if (string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(request.ClientSecretReference)) {
                var (sourceProfileId, sourceSecretName) = ParseSecretReference(request.ClientSecretReference!, request.ProfileId);
                clientSecret = await _secretStore.GetSecretAsync(sourceProfileId, sourceSecretName, cancellationToken);
            }
            var account = request.GmailAccount
                ?? (savedProfile.Settings.TryGetValue(MailProfileSettingsKeys.Mailbox, out var mailbox) ? mailbox : null)
                ?? "user@gmail.com";
            LastGmailRequest = new GmailProfileLoginRequest {
                ProfileId = request.ProfileId,
                GmailAccount = account,
                ClientId = request.ClientId ?? (savedProfile.Settings.TryGetValue(MailProfileSettingsKeys.ClientId, out var clientId) ? clientId : null),
                ClientSecret = clientSecret,
                Scopes = request.Scopes
            };
            savedProfile.Settings[MailProfileSettingsKeys.Mailbox] = account;
            savedProfile.Settings[MailProfileSettingsKeys.ClientId] = request.ClientId ?? savedProfile.Settings[MailProfileSettingsKeys.ClientId];
            savedProfile.DefaultMailbox = account;
            await _profileStore.SaveAsync(savedProfile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "gmail-access-token", cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.RefreshToken, "gmail-refresh-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Gmail login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Gmail,
                UserName = account
            };
        }

        public async Task<MailProfileAuthenticationResult> LoginGraphAsync(GraphProfileLoginRequest request, CancellationToken cancellationToken = default) {
            LastGraphRequest = request;
            var profile = await _profileStore.GetByIdAsync(request.ProfileId, cancellationToken);
            var mailbox = request.Mailbox ?? request.Login ?? "user@example.com";
            profile!.Settings[MailProfileSettingsKeys.Mailbox] = mailbox;
            profile.Settings[MailProfileSettingsKeys.RedirectUri] = request.RedirectUri ?? "https://login.microsoftonline.com/common/oauth2/nativeclient";
            profile.DefaultMailbox = mailbox;
            await _profileStore.SaveAsync(profile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "graph-access-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Graph login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Graph,
                UserName = request.Login ?? mailbox
            };
        }

        public async Task<MailProfileAuthenticationResult> RefreshAsync(string profileId, CancellationToken cancellationToken = default) {
            RefreshCalls++;
            LastRefreshProfileId = profileId;
            var profile = await _profileStore.GetByIdAsync(profileId, cancellationToken);
            Assert.NotNull(profile);
            return profile!.Kind switch {
                MailProfileKind.Gmail => await LoginGmailAsync(new GmailProfileLoginRequest { ProfileId = profileId }, cancellationToken),
                MailProfileKind.Graph => await LoginGraphAsync(new GraphProfileLoginRequest { ProfileId = profileId }, cancellationToken),
                _ => new MailProfileAuthenticationResult {
                    Succeeded = false,
                    Code = "refresh_not_supported",
                    Message = "Refresh not supported.",
                    ProfileId = profileId,
                    ProfileKind = profile.Kind
                }
            };
        }

        private static (string ProfileId, string SecretName) ParseSecretReference(string secretReference, string defaultProfileId) {
            var normalized = secretReference.Trim();
            var colonIndex = normalized.IndexOf(':');
            var slashIndex = normalized.IndexOf('/');
            var separatorIndex = colonIndex >= 0 && slashIndex >= 0
                ? Math.Min(colonIndex, slashIndex)
                : Math.Max(colonIndex, slashIndex);

            if (separatorIndex < 0) {
                return (defaultProfileId, normalized);
            }

            return (normalized[..separatorIndex], normalized[(separatorIndex + 1)..]);
        }
    }

    private sealed class FakeProfileConnectionService : IMailProfileConnectionService {
        public string? LastProfileId { get; private set; }

        public MailProfileConnectionTestScope LastScope { get; private set; }

        public Task<MailProfileConnectionTestResult> TestAsync(
            string profileId,
            MailProfileConnectionTestScope scope = MailProfileConnectionTestScope.Auto,
            CancellationToken cancellationToken = default) {
            LastProfileId = profileId;
            LastScope = scope;
            return Task.FromResult(new MailProfileConnectionTestResult {
                Succeeded = true,
                Message = "Profile connection succeeded.",
                ProfileId = profileId,
                ProfileKind = MailProfileKind.Imap,
                Probe = "connect",
                Target = "work@example.com",
                RequestedScope = scope,
                ExecutedScope = scope == MailProfileConnectionTestScope.Auto ? MailProfileConnectionTestScope.Auth : scope,
                Stages = new List<MailProfileConnectionTestStage> {
                    new() {
                        Phase = MailProfileConnectionTestPhase.Profile,
                        Succeeded = true,
                        Probe = "resolveProfile",
                        Target = profileId,
                        DurationMilliseconds = 3,
                        Message = "Profile resolved.",
                        Evidence = new MailProfileDiagnosticEvidence {
                            Protocol = "IMAP",
                            Session = new MailProfileSessionEvidence {
                                Connected = true,
                                Authenticated = true,
                                Secure = true,
                                TlsProtocol = "Tls13",
                                Capabilities = new List<string> { "Idle" },
                                AuthenticationMechanisms = new List<string> { "XOAUTH2" }
                            }
                        }
                    }
                }
            });
        }
    }

    private sealed class TestApplicationFixture {
        public TestApplicationFixture() {
            ProfileStore = new InMemoryProfileStore(new[] {
                new MailProfile {
                    Id = "work-imap",
                    DisplayName = "Work IMAP",
                    Kind = MailProfileKind.Imap,
                    Settings = new Dictionary<string, string> {
                        [MailProfileSettingsKeys.Server] = "imap.example.com"
                    }
                }
            });
            SecretStore = new InMemorySecretStore();
            SecretStore.SetSecretAsync("work-imap", MailSecretNames.Password, "secret").GetAwaiter().GetResult();
            ProfileAuthService = new FakeProfileAuthService(ProfileStore, SecretStore);
        }

        public InMemoryProfileStore ProfileStore { get; }

        public InMemorySecretStore SecretStore { get; }

        public FakeReadService ReadService { get; } = new();

        public FakeEmlExportService EmlExportService { get; } = new();

        public FakeQueueService QueueService { get; } = new();

        public FakeSendService SendService { get; } = new();

        public FakeMessageActionService MessageActionService { get; } = new();

        public FakeMessageActionPlanExchangeService MessageActionPlanExchangeService { get; } = new();

        public FakeMessageActionPlanRegistryService MessageActionPlanRegistryService { get; } = new();

        public FakeDraftService DraftService { get; } = new();

        public FakeDraftExchangeService DraftExchangeService { get; } = new();

        public FakeProfileAuthService ProfileAuthService { get; }

        public FakeProfileConnectionService ProfileConnectionService { get; } = new();

        public MailApplicationBuilder CreateBuilder() =>
            new MailApplicationBuilder()
                .UseProfileStore(ProfileStore)
                .UseSecretStore(SecretStore)
                .UseDraftStore(new FileMailDraftStore(CreateTemporaryFilePath("drafts.json")))
                .UseProfileService(new MailProfileService(ProfileStore, SecretStore))
                .UseProfileConnectionService(ProfileConnectionService)
                .UseProfileSecretService(new MailProfileSecretService(ProfileStore, SecretStore))
                .UseProfileAuthService(ProfileAuthService)
                .UseDraftService(DraftService)
                .UseDraftExchangeService(DraftExchangeService)
                .UseReadService(ReadService)
                .UseEmlExportService(EmlExportService)
                .UseMessageActionService(MessageActionService)
                .UseMessageActionPlanExchangeService(MessageActionPlanExchangeService)
                .UseMessageActionPlanRegistryService(MessageActionPlanRegistryService)
                .UseQueueService(QueueService)
                .UseSendService(SendService);

        private static string CreateTemporaryFilePath(string fileName) {
            var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
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

    private static string CreateTemporaryFilePath(string fileName) {
        var directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, fileName);
    }
}
#endif
