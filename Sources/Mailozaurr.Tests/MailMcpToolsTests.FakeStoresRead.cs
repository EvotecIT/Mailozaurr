#if NET8_0_OR_GREATER
using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;

namespace Mailozaurr.Tests;

public sealed partial class MailMcpToolsTests {
    private sealed class InMemoryProfileStore : IMailProfileStore {
        private readonly Dictionary<string, MailProfile> _profiles;

        public InMemoryProfileStore(IEnumerable<MailProfile> profiles) {
            _profiles = profiles.ToDictionary(profile => profile.Id, CloneProfile, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<MailProfile>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MailProfile>>(_profiles.Values.Select(CloneProfile).ToArray());

        public Task<MailProfile?> GetByIdAsync(string profileId, CancellationToken cancellationToken = default) {
            _profiles.TryGetValue(profileId, out var profile);
            return Task.FromResult(profile == null ? null : CloneProfile(profile));
        }

        public Task SaveAsync(MailProfile profile, CancellationToken cancellationToken = default) {
            _profiles[profile.Id] = CloneProfile(profile);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(string profileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_profiles.Remove(profileId));

        private static MailProfile CloneProfile(MailProfile profile) => new() {
            Id = profile.Id,
            DisplayName = profile.DisplayName,
            Description = profile.Description,
            Kind = profile.Kind,
            DefaultSender = profile.DefaultSender,
            DefaultMailbox = profile.DefaultMailbox,
            IsDefault = profile.IsDefault,
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.OrdinalIgnoreCase),
            Capabilities = profile.Capabilities == null
                ? null
                : new ProfileCapabilities(profile.Capabilities.Kind, profile.Capabilities.Capabilities)
        };
    }

    private sealed class InMemorySecretStore : IMailSecretStore {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) {
            _secrets.TryGetValue(CreateKey(profileId, secretName), out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetSecretAsync(string profileId, string secretName, string secretValue, CancellationToken cancellationToken = default) {
            _secrets[CreateKey(profileId, secretName)] = secretValue;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveSecretAsync(string profileId, string secretName, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.Remove(CreateKey(profileId, secretName)));

        private static string CreateKey(string profileId, string secretName) => $"{profileId}::{secretName}";
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

        public MailSearchRequest? LastSearchRequest { get; private set; }

        public Task<IReadOnlyList<FolderRefCompact>> GetFoldersCompactAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderCompactQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRefCompact>>(new[] {
                new FolderRefCompact {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "Inbox",
                    DisplayName = "Inbox",
                    Path = "Inbox",
                    Summary = "Inbox Inbox"
                }
            });
        }

        public Task<IReadOnlyList<FolderRef>> GetFoldersAsync(MailFolderQuery query, CancellationToken cancellationToken = default) {
            LastFolderQuery = query;
            return Task.FromResult<IReadOnlyList<FolderRef>>(new[] {
                new FolderRef {
                    ProfileId = query.ProfileId,
                    MailboxId = query.MailboxId,
                    Id = "Inbox",
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

        public Task<IReadOnlyList<MessageSummary>> SearchAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummary>>(new[] {
                new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = "message-1",
                    Subject = "Quarterly invoice"
                }
            });
        }

        public Task<IReadOnlyList<MessageSummaryCompact>> SearchCompactAsync(MailSearchRequest request, CancellationToken cancellationToken = default) {
            LastSearchCompactRequest = request;
            return Task.FromResult<IReadOnlyList<MessageSummaryCompact>>(new[] {
                new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = "message-1",
                    Subject = "Quarterly invoice",
                    Summary = "message-1 Quarterly invoice"
                }
            });
        }

        public Task<IReadOnlyList<AttachmentSummary>> GetAttachmentsAsync(ListAttachmentsRequest request, CancellationToken cancellationToken = default) {
            LastListAttachmentsRequest = request;
            return Task.FromResult<IReadOnlyList<AttachmentSummary>>(new[] {
                new AttachmentSummary {
                    MessageId = request.MessageId,
                    Id = "attachment-1",
                    FileName = "invoice.pdf",
                    SizeInBytes = 4096
                }
            });
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
                        AttachmentId = "attachment-1",
                        FileName = "invoice.pdf",
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

        public Task<MessageDetailCompact?> GetMessageCompactAsync(GetMessageRequest request, CancellationToken cancellationToken = default) {
            LastGetCompactRequest = request;
            return Task.FromResult<MessageDetailCompact?>(new MessageDetailCompact {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummaryCompact {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Retrieved message",
                    Summary = $"{request.MessageId} Retrieved message"
                },
                TextBodyPreview = "Preview",
                HtmlBodyPreview = "<p>Preview</p>",
                HasRawContent = request.IncludeRawContent,
                SummaryText = $"{request.MessageId} Retrieved message"
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
                    Subject = "Retrieved message",
                    Summary = $"{messageId} Retrieved message"
                },
                TextBodyPreview = "Preview",
                HtmlBodyPreview = "<p>Preview</p>",
                HasRawContent = request.IncludeRawContent,
                SummaryText = $"{messageId} Retrieved message"
            }).ToArray());
        }

        public Task<MessageDetail?> GetMessageAsync(GetMessageRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<MessageDetail?>(new MessageDetail {
                ProfileId = request.ProfileId,
                Id = request.MessageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = request.MessageId,
                    Subject = "Retrieved message"
                }
            });

        public Task<IReadOnlyList<MessageDetail>> GetMessagesAsync(GetMessagesRequest request, CancellationToken cancellationToken = default) {
            LastGetManyRequest = request;
            return Task.FromResult<IReadOnlyList<MessageDetail>>(request.MessageIds.Select(messageId => new MessageDetail {
                ProfileId = request.ProfileId,
                Id = messageId,
                Summary = new MessageSummary {
                    ProfileId = request.ProfileId,
                    Id = messageId,
                    Subject = "Retrieved message"
                }
            }).ToArray());
        }

        public Task<OperationResult> SaveAttachmentAsync(SaveAttachmentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Success("Attachment saved."));
    }
}
#endif