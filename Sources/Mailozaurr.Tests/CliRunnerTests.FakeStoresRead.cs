#if NET8_0_OR_GREATER
using System.Text.Json;
using Mailozaurr;
using Mailozaurr.Cli;

namespace Mailozaurr.Tests;

public sealed partial class CliRunnerTests {
    private static TestApplicationFixture CreateFixture() => new();

    private sealed class InMemoryProfileStore : IMailProfileStore, IMailProfileMaintenanceCoordinator {
        private readonly SemaphoreSlim _writerGate = new(1, 1);
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile);
        }

        public async Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) {
            await _writerGate.WaitAsync(cancellationToken);
            try {
                return _profiles.Remove(profileId);
            } finally {
                _writerGate.Release();
            }
        }

        public async Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            await _writerGate.WaitAsync(cancellationToken);
            try {
                _profiles[profile.Id] = profile;
            } finally {
                _writerGate.Release();
            }
        }

        public async Task<TResult> ExecuteWithStableProfileIdsAsync<TResult>(
            Func<IReadOnlyCollection<string>, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) {
            await _writerGate.WaitAsync(cancellationToken);
            try {
                return await operation(_profiles.Keys.ToArray(), cancellationToken);
            } finally {
                _writerGate.Release();
            }
        }
    }

    private sealed class InMemorySecretStore : IMailSecretStore, IMailProfileSecretMaintenanceStore {
        private readonly Dictionary<string, Dictionary<string, string>> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            if (_secrets.TryGetValue(profileId, out var profileSecrets) &&
                profileSecrets.TryGetValue(secretName, out var secretValue)) {
                return Task.FromResult<string?>(secretValue);
            }

            return Task.FromResult<string?>(null);
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            var removed = _secrets.TryGetValue(profileId, out var profileSecrets) &&
                          profileSecrets.Remove(secretName);
            return Task.FromResult(removed);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            if (!_secrets.TryGetValue(profileId, out var profileSecrets)) {
                profileSecrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _secrets[profileId] = profileSecrets;
            }

            profileSecrets[secretName] = secretValue;
            return Task.CompletedTask;
        }

        public Task<MailProfileSecretMaintenanceResult> InspectOrphanedSecretsAsync(
            IReadOnlyCollection<string> knownProfileIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMaintenanceResult(knownProfileIds, remove: false));

        public Task<MailProfileSecretMaintenanceResult> RemoveOrphanedSecretsAsync(
            IReadOnlyCollection<string> knownProfileIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMaintenanceResult(knownProfileIds, remove: true));

        private MailProfileSecretMaintenanceResult CreateMaintenanceResult(
            IReadOnlyCollection<string> knownProfileIds,
            bool remove) {
            var knownProfiles = new HashSet<string>(knownProfileIds, StringComparer.OrdinalIgnoreCase);
            string[] orphans = _secrets.Keys
                .Where(profileId => !knownProfiles.Contains(profileId))
                .OrderBy(profileId => profileId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var result = new MailProfileSecretMaintenanceResult { Succeeded = true };
            result.OrphanedProfileIds.AddRange(orphans);
            if (remove) {
                foreach (string orphan in orphans) {
                    _secrets.Remove(orphan);
                    result.RemovedProfileIds.Add(orphan);
                }
            }
            return result;
        }
    }

    private sealed class FakeReadService : IMailReadService {
        public MailFolderQuery? LastFolderQuery { get; private set; }

        public GetMessageRequest? LastGetCompactRequest { get; private set; }

        public GetMessagesRequest? LastGetManyCompactRequest { get; private set; }

        public GetMessagesRequest? LastGetManyRequest { get; private set; }

        public SaveAttachmentsManyRequest? LastSaveAttachmentsManyRequest { get; private set; }

        public SaveAttachmentsRequest? LastSaveAttachmentsRequest { get; private set; }

        public ListAttachmentsRequest? LastListAttachmentsRequest { get; private set; }

        public MailFolderQuery? LastFolderCompactQuery { get; private set; }

        public MailSearchRequest? LastSearchCompactRequest { get; private set; }

        public GetMessageRequest? LastGetRequest { get; private set; }

        public SaveAttachmentRequest? LastSaveAttachmentRequest { get; private set; }

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(CaptureGetRequest(request));

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
            LastGetCompactRequest = request;
            return Task.FromResult<MessageDetailCompact?>(new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Subject",
                    Summary = $"{request.MessageId} Subject"
                },
                TextBodyPreview = "Body",
                SummaryText = $"{request.MessageId} Subject"
            });
        }

        public Task<IReadOnlyList<MessageDetailCompact>> GetMessagesCompactAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
            LastGetManyCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageDetailCompact>>(request.MessageIds.Select(messageId => new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = messageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = messageId,
                    Subject = "Subject",
                    Summary = $"{messageId} Subject"
                },
                TextBodyPreview = "Body",
                SummaryText = $"{messageId} Subject"
            }).ToArray());
        }

        private MessageDetail CaptureGetRequest(GetMessageRequest request) {
            LastGetRequest = request;
            return new MessageDetail {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Subject"
                },
                TextBody = "Body"
            };
        }

        public Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
            LastGetManyRequest = request;
            return Task.FromResult<IReadOnlyList<MessageDetail>>(request.MessageIds.Select(messageId => new MessageDetail {
                ProfileId = request.ProfileId,
                Id = messageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = messageId,
                    Subject = "Subject"
                },
                TextBody = "Body"
            }).ToArray());
        }

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderCompactQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRefCompact>>(new[] {
                new FolderRefCompact {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    Summary = "inbox Inbox"
                }
            });
        }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    SpecialUse = "inbox"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "archive",
                    DisplayName = "Archive",
                    Path = "Archive",
                    SpecialUse = "archive"
                },
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "trash",
                    DisplayName = "Trash",
                    Path = "Trash",
                    SpecialUse = "trash"
                }
            });
        }

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentRequest = request;
            return Task.FromResult(OperationResult.Success("Attachment saved."));
        }

        public Task<SaveAttachmentsResult> SaveAttachmentsAsync(SaveAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentsRequest = request;
            return Task.FromResult(new SaveAttachmentsResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                MessageId = request.MessageId,
                MatchedCount = 1,
                AttemptedCount = 1,
                SavedCount = 1,
                FailedCount = 0,
                Message = "Saved 1 attachment(s).",
                Results = {
                    new SavedAttachmentResult {
                        Succeeded = true,
                        AttachmentId = "att-1",
                        FileName = "report.pdf",
                        ContentType = "application/pdf"
                    }
                }
            });
        }

        public Task<SaveAttachmentsManyResult> SaveAttachmentsManyAsync(SaveAttachmentsManyRequest request, CancellationToken cancellationToken = default) {
            LastSaveAttachmentsManyRequest = request;
            return Task.FromResult(new SaveAttachmentsManyResult {
                Succeeded = true,
                ProfileId = request.ProfileId,
                RequestedMessageCount = request.MessageIds.Count,
                AttemptedMessageCount = request.MessageIds.Count,
                SucceededMessageCount = request.MessageIds.Count,
                MatchedCount = request.MessageIds.Count,
                AttemptedCount = request.MessageIds.Count,
                SavedCount = request.MessageIds.Count,
                FailedCount = 0,
                Message = $"Saved {request.MessageIds.Count} attachment(s) across {request.MessageIds.Count} message(s).",
                MessageResults = request.MessageIds.Select(messageId => new SaveAttachmentsResult {
                    Succeeded = true,
                    ProfileId = request.ProfileId,
                    MessageId = messageId,
                    MatchedCount = 1,
                    AttemptedCount = 1,
                    SavedCount = 1,
                    FailedCount = 0,
                    Message = "Saved 1 attachment(s)."
                }).ToList()
            });
        }

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(new[] {
                new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = "msg-1",
                    Subject = request.QueryText,
                    Summary = $"msg-1 {request.QueryText}"
                }
            });
        }

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastListAttachmentsRequest = request;
            return Task.FromResult<IReadOnlyList<AttachmentSummary>>(new[] {
                new AttachmentSummary {
                    MessageId = request.MessageId,
                    Id = "att-1",
                    FileName = "report.pdf",
                    SizeInBytes = 2048
                }
            });
        }

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = "msg-1",
                    Subject = request.QueryText
                }
            });
    }
}
#endif