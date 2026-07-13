#if NET8_0_OR_GREATER
using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    private sealed class FakeSendService : IMailSendService {
        public SendMessageRequest? LastRequest { get; private set; }

        public Task<SendResult> SendAsync(SendMessageRequest request, CancellationToken cancellationToken = default) {
            LastRequest = request;
            return Task.FromResult(new SendResult {
                Succeeded = true,
                Queued = false,
                ProviderMessageId = "provider-1",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Gmail,
                Message = "Send handled."
            });
        }
    }

    private sealed class FakeMessageActionService : IMailMessageActionService {
        public SetReadStateRequest? LastSetReadStateRequest { get; private set; }

        public SetFlaggedStateRequest? LastSetFlaggedStateRequest { get; private set; }

        public MoveMessagesRequest? LastMoveRequest { get; private set; }

        public DeleteMessagesRequest? LastDeleteRequest { get; private set; }

        public Task<MessageActionResult> SetReadStateAsync(SetReadStateRequest request, CancellationToken cancellationToken = default) {
            LastSetReadStateRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, request.IsRead ? "Marked messages as read." : "Marked messages as unread."));
        }

        public Task<MessageActionResult> SetFlaggedStateAsync(SetFlaggedStateRequest request, CancellationToken cancellationToken = default) {
            LastSetFlaggedStateRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, request.IsFlagged ? "Flagged messages." : "Unflagged messages."));
        }

        public Task<MessageActionResult> MoveAsync(MoveMessagesRequest request, CancellationToken cancellationToken = default) {
            LastMoveRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, $"Moved messages to '{request.DestinationFolderId}'."));
        }

        public Task<MessageActionResult> DeleteAsync(DeleteMessagesRequest request, CancellationToken cancellationToken = default) {
            LastDeleteRequest = request;
            return Task.FromResult(CreateResult(request.ProfileId, request.MessageIds, "Deleted messages."));
        }

        private static MessageActionResult CreateResult(string profileId, IReadOnlyList<string> messageIds, string message) => new() {
            Succeeded = true,
            ProfileId = profileId,
            RequestedCount = messageIds.Count,
            SucceededCount = messageIds.Count,
            FailedCount = 0,
            Message = message,
            Results = messageIds.Select(id => new MessageActionItemResult {
                MessageId = id,
                Succeeded = true
            }).ToList()
        };
    }

    private sealed class FakeQueueService : IMailQueueService {
        public bool ProcessCalled { get; private set; }

        public Task<IReadOnlyList<QueuedMessageCompact>> ListCompactAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageCompact>>(new[] {
                new QueuedMessageCompact {
                    MessageId = "queued-1",
                    Provider = "gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    AttemptCount = 0,
                    IsDue = true,
                    HasProviderData = false,
                    Summary = "queued-1 [gmail] attempts=0"
                }
            });

        public Task<IReadOnlyList<QueuedMessageSummary>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(new[] {
                new QueuedMessageSummary {
                    MessageId = "queued-1",
                    Provider = "gmail",
                    ProfileKind = MailProfileKind.Gmail,
                    QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    NextAttemptAt = DateTimeOffset.UtcNow,
                    AttemptCount = 0
                }
            });

        public Task<QueuedMessageCompact?> GetCompactAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageCompact?>(new QueuedMessageCompact {
                MessageId = messageId,
                Provider = "gmail",
                ProfileKind = MailProfileKind.Gmail,
                NextAttemptAt = DateTimeOffset.UtcNow,
                AttemptCount = 0,
                IsDue = true,
                HasProviderData = false,
                Summary = $"{messageId} [gmail] attempts=0"
            });

        public Task<QueuedMessageSummary?> GetAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageSummary?>(new QueuedMessageSummary {
                MessageId = messageId,
                Provider = "gmail",
                ProfileKind = MailProfileKind.Gmail,
                QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                NextAttemptAt = DateTimeOffset.UtcNow,
                AttemptCount = 0
            });

        public Task<OperationResult> RemoveAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Queued message removed."));

        public Task<IReadOnlyList<QueuedMessageSummary>> ListDeadLettersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<QueuedMessageSummary>>(Array.Empty<QueuedMessageSummary>());

        public Task<QueuedMessageSummary?> GetDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult<QueuedMessageSummary?>(null);

        public Task<OperationResult> RemoveDeadLetterAsync(string messageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Dead-lettered message removed."));

        public Task<QueueProcessResult> ProcessAsync(CancellationToken cancellationToken = default) {
            ProcessCalled = true;
            return Task.FromResult(new QueueProcessResult {
                Succeeded = true,
                AttemptedCount = 1,
                SentCount = 1,
                FailedCount = 0,
                SkippedCount = 0,
                DroppedCount = 0,
                Message = "Queue processed."
            });
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
            return new MailProfileAuthStatus {
                ProfileId = profile.Id,
                ProfileKind = profile.Kind,
                AuthFlow = profile.Settings.TryGetValue(MailProfileSettingsKeys.AuthFlow, out var authFlow) ? authFlow : MailProfileAuthFlowNames.Interactive,
                Mode = profile.Kind == MailProfileKind.Graph ? "appOnly" : "interactive",
                Mailbox = profile.DefaultMailbox,
                HasAccessToken = !string.IsNullOrWhiteSpace(accessToken),
                HasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
                HasClientSecret = !string.IsNullOrWhiteSpace(clientSecret),
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
            profile.DefaultMailbox = mailbox;
            await _profileStore.SaveAsync(profile, cancellationToken);
            await _secretStore.SetSecretAsync(request.ProfileId, MailSecretNames.AccessToken, "graph-access-token", cancellationToken);
            return new MailProfileAuthenticationResult {
                Succeeded = true,
                Message = "Graph login completed.",
                ProfileId = request.ProfileId,
                ProfileKind = MailProfileKind.Graph,
                UserName = mailbox
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
                ProfileKind = MailProfileKind.Gmail,
                Probe = "getProfile",
                Target = "gmail-work",
                RequestedScope = scope,
                ExecutedScope = scope == MailProfileConnectionTestScope.Auto ? MailProfileConnectionTestScope.Mailbox : scope
            });
        }
    }
}
#endif
