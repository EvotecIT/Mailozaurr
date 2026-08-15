using Mailozaurr;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static async Task<int> WriteUnknownCommandAsync(string command, TextWriter error) {
        await error.WriteLineAsync($"Unknown command '{command}'.").ConfigureAwait(false);
        return 1;
    }

    private static string RequireOption(CliArguments parseResult, string name) {
        var value = parseResult.GetOption(name);
        if (!string.IsNullOrWhiteSpace(value)) {
            return value!;
        }

        throw new InvalidOperationException($"Missing required option '--{name}'.");
    }

    private static MailProfile BuildProfile(CliArguments parseResult) {
        var profile = new MailProfile {
            Id = RequireOption(parseResult, "profile"),
            DisplayName = RequireOption(parseResult, "name"),
            Kind = MailProfileKindParser.Parse(RequireOption(parseResult, "kind")),
            Description = parseResult.GetOption("description"),
            DefaultSender = parseResult.GetOption("default-sender"),
            DefaultMailbox = parseResult.GetOption("default-mailbox"),
            IsDefault = parseResult.HasFlag("is-default")
        };

        foreach (var setting in parseResult.GetOptionValues("setting")) {
            if (string.IsNullOrWhiteSpace(setting)) {
                continue;
            }

            var separatorIndex = setting.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == setting.Length - 1) {
                throw new InvalidOperationException("Option '--setting' must use the format key=value.");
            }

            var key = setting[..separatorIndex].Trim();
            var value = setting[(separatorIndex + 1)..].Trim();
            if (key.Length == 0 || value.Length == 0) {
                throw new InvalidOperationException("Option '--setting' must use the format key=value.");
            }

            profile.Settings[key] = value;
        }

        return profile;
    }

    private static MessageActionExecutionPlanRequest BuildMessageActionExecutionPlanRequest(CliArguments parseResult) => new() {
        Action = RequireOption(parseResult, "action"),
        ProfileId = RequireOption(parseResult, "profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        MessageIds = parseResult.GetOptionValues("message-id")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        DestinationFolderId = parseResult.GetOption("target-folder"),
        ConfirmationToken = parseResult.GetOption("confirm-token")
    };

    private static CommonMessageActionsPreviewRequest BuildCommonActionsPreviewRequest(CliArguments parseResult) => new() {
        ProfileId = RequireOption(parseResult, "profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        MessageIds = parseResult.GetOptionValues("message-id")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        DestinationFolderId = parseResult.GetOption("target-folder")
    };

    private static MessageActionPlanBatchTransformRequest BuildMessageActionPlanBatchTransformRequest(CliArguments parseResult) => new() {
        PlanIndexes = parseResult.GetIntOptionValues("index").ToList(),
        PlanNames = parseResult.GetOptionValues("plan-name")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList(),
        ProfileId = parseResult.GetOption("target-profile"),
        MailboxId = parseResult.GetOption("mailbox"),
        FolderId = parseResult.GetOption("folder"),
        DestinationFolderId = parseResult.GetOption("target-folder")
    };

    private static MailMessageActionPlanBatchQuery? BuildMessageActionPlanBatchQuery(CliArguments parseResult) {
        var planNames = parseResult.GetOptionValues("plan-name")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var profileIds = parseResult.GetOptionValues("profile")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var actions = parseResult.GetOptionValues("action")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var hasExplicitSort = parseResult.GetOptionValues("sort").Count > 0;
        var sortBy = ParseBatchSortBy(parseResult.GetOption("sort"));
        var descending = parseResult.HasFlag("desc");

        if (planNames.Count == 0 && profileIds.Count == 0 && actions.Count == 0 && !hasExplicitSort && sortBy == MailMessageActionPlanBatchSortBy.Id && !descending) {
            return null;
        }

        return new MailMessageActionPlanBatchQuery {
            PlanNames = planNames,
            ProfileIds = profileIds,
            Actions = actions,
            SortBy = sortBy,
            Descending = descending
        };
    }

    private static MailMessageActionPlanBatchSortBy ParseBatchSortBy(string? rawSortBy) {
        if (string.IsNullOrWhiteSpace(rawSortBy)) {
            return MailMessageActionPlanBatchSortBy.Id;
        }

        return rawSortBy.Trim().ToLowerInvariant() switch {
            "id" => MailMessageActionPlanBatchSortBy.Id,
            "name" => MailMessageActionPlanBatchSortBy.Name,
            "plans" or "plan-count" => MailMessageActionPlanBatchSortBy.PlanCount,
            "ready" or "ready-count" => MailMessageActionPlanBatchSortBy.ReadyPlanCount,
            "updated" or "updated-at" => MailMessageActionPlanBatchSortBy.UpdatedAt,
            "actions" or "action-types" => MailMessageActionPlanBatchSortBy.ActionTypeCount,
            _ => throw new InvalidOperationException($"Unsupported batch sort '{rawSortBy}'.")
        };
    }

    private static async Task<SendMessageRequest> BuildSendRequestAsync(MailApplication application, CliArguments parseResult) {
        if (application == null) {
            throw new ArgumentNullException(nameof(application));
        }

        var existingDraftId = parseResult.GetOption("draft");
        var draftFilePath = parseResult.GetOption("file");
        if (!string.IsNullOrWhiteSpace(existingDraftId) && !string.IsNullOrWhiteSpace(draftFilePath)) {
            throw new InvalidOperationException("Options '--draft' and '--file' cannot be used together.");
        }

        var queueOnFailure = parseResult.HasFlag("queue-on-failure");
        if (!string.IsNullOrWhiteSpace(draftFilePath)) {
            var importedDraft = await application.DraftExchange.LoadAsync(draftFilePath!).ConfigureAwait(false);
            return CreateSendRequestFromDraft(importedDraft.Message, queueOnFailure);
        }

        if (!string.IsNullOrWhiteSpace(existingDraftId)) {
            var existingDraft = await application.Drafts.GetDraftAsync(existingDraftId!).ConfigureAwait(false);
            if (existingDraft == null) {
                throw new InvalidOperationException($"Draft '{existingDraftId}' was not found.");
            }

            return CreateSendRequestFromDraft(existingDraft.Message, queueOnFailure);
        }

        var profileId = RequireOption(parseResult, "profile");
        var draft = BuildDraftMessage(parseResult, profileId);

        return new SendMessageRequest {
            ProfileId = profileId,
            Message = draft,
            QueueOnFailure = queueOnFailure
        };
    }

    private static async Task<MailDraft> BuildMailDraftAsync(MailApplication application, CliArguments parseResult) {
        if (application == null) {
            throw new ArgumentNullException(nameof(application));
        }

        var filePath = parseResult.GetOption("file");
        if (!string.IsNullOrWhiteSpace(filePath)) {
            var importedDraft = await application.DraftExchange.LoadAsync(filePath!).ConfigureAwait(false);
            var overrideDraftId = parseResult.GetOption("draft");
            var overrideName = parseResult.GetOption("name");
            if (!string.IsNullOrWhiteSpace(overrideDraftId)) {
                importedDraft.Id = overrideDraftId!.Trim();
            }
            if (!string.IsNullOrWhiteSpace(overrideName)) {
                importedDraft.Name = overrideName!.Trim();
            }

            return importedDraft;
        }

        var draftId = RequireOption(parseResult, "draft");
        var draftName = RequireOption(parseResult, "name");

        return new MailDraft {
            Id = draftId,
            Name = draftName,
            Message = BuildDraftMessage(parseResult, RequireOption(parseResult, "profile"))
        };
    }

    private static SendMessageRequest CreateSendRequestFromDraft(DraftMessage draft, bool queueOnFailure) =>
        new() {
            ProfileId = draft.ProfileId,
            Message = DraftMessageCloner.Clone(draft),
            QueueOnFailure = queueOnFailure
        };

    private static MessageRecipient ToRecipient(string value) => new() {
        Address = value.Trim()
    };

    private static DraftMessage BuildDraftMessage(CliArguments parseResult, string profileId) {
        var draft = new DraftMessage {
            ProfileId = profileId,
            Subject = parseResult.GetOption("subject"),
            TextBody = parseResult.GetOption("text"),
            HtmlBody = parseResult.GetOption("html")
        };

        var from = parseResult.GetOption("from");
        if (!string.IsNullOrWhiteSpace(from)) {
            draft.From = ToRecipient(from!);
        }

        draft.To.AddRange(parseResult.GetOptionValues("to").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.Cc.AddRange(parseResult.GetOptionValues("cc").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.Bcc.AddRange(parseResult.GetOptionValues("bcc").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));
        draft.ReplyTo.AddRange(parseResult.GetOptionValues("reply-to").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => ToRecipient(value!)));

        foreach (var header in parseResult.GetOptionValues("header")) {
            if (string.IsNullOrWhiteSpace(header)) {
                continue;
            }

            var separatorIndex = header.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == header.Length - 1) {
                throw new InvalidOperationException("Option '--header' must use the format key=value.");
            }

            draft.Headers[header[..separatorIndex].Trim()] = header[(separatorIndex + 1)..].Trim();
        }

        foreach (var attachmentPath in parseResult.GetOptionValues("attachment")) {
            if (string.IsNullOrWhiteSpace(attachmentPath)) {
                continue;
            }

            draft.Attachments.Add(new DraftAttachment {
                Path = attachmentPath!.Trim()
            });
        }

        return draft;
    }

    private static async Task WriteItemAsync<T>(
        TextWriter output,
        T value,
        bool json,
        Func<T, string> formatter) {
        if (json) {
            await output.WriteLineAsync(SerializeJson(value)).ConfigureAwait(false);
            return;
        }

        await output.WriteLineAsync(formatter(value)).ConfigureAwait(false);
    }

    private static async Task WriteSequenceAsync<T>(
        TextWriter output,
        IReadOnlyList<T> values,
        bool json,
        Func<T, string> formatter) {
        if (json) {
            await output.WriteLineAsync(SerializeJson<IReadOnlyList<T>>(values)).ConfigureAwait(false);
            return;
        }

        foreach (var value in values) {
            await output.WriteLineAsync(formatter(value)).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ResolveSensitiveOptionAsync(
        CliArguments parseResult,
        string optionName,
        TextReader input,
        bool required = false) {
        var envName = parseResult.GetOption($"{optionName}-env");
        var stdinRequested = parseResult.HasFlag($"{optionName}-stdin");

        var sourceCount = 0;
        if (!string.IsNullOrWhiteSpace(envName)) {
            sourceCount++;
        }
        if (stdinRequested) {
            sourceCount++;
        }

        if (sourceCount > 1) {
            throw new InvalidOperationException($"Option '--{optionName}' accepts only one secret source at a time.");
        }

        string? resolvedValue = null;
        if (!string.IsNullOrWhiteSpace(envName)) {
            resolvedValue = Environment.GetEnvironmentVariable(envName!);
            if (resolvedValue == null) {
                throw new InvalidOperationException($"Environment variable '{envName}' was not found for '--{optionName}-env'.");
            }
        } else if (stdinRequested) {
            resolvedValue = await input.ReadToEndAsync().ConfigureAwait(false);
            resolvedValue = resolvedValue.TrimEnd('\r', '\n');
        }

        if (required && resolvedValue == null) {
            throw new InvalidOperationException(
                $"Missing required secret source. Use '--{optionName}-env <name>' or '--{optionName}-stdin'.");
        }

        return resolvedValue;
    }

    private static void ValidateSingleStdinSecretSource(CliArguments parseResult, params string[] optionNames) {
        var stdinOptions = optionNames
            .Where(optionName => parseResult.HasFlag($"{optionName}-stdin"))
            .Select(optionName => $"--{optionName}-stdin")
            .ToArray();
        if (stdinOptions.Length > 1) {
            throw new InvalidOperationException(
                $"Standard input can supply only one secret per command. Requested: {string.Join(", ", stdinOptions)}.");
        }
    }

    private static async Task WriteExceptionAsync(TextWriter error, Exception exception, bool json) {
        if (json) {
            var payload = new CliErrorEnvelope {
                Error = new CliError {
                    Type = exception.GetType().Name,
                    Message = exception.Message
                }
            };
            await error.WriteLineAsync(SerializeJson(payload)).ConfigureAwait(false);
            return;
        }

        await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
    }

    private static string SerializeJson<T>(T value) {
        var typeInfo = CliJsonContext.Default.GetTypeInfo(typeof(T)) ??
            throw new InvalidOperationException($"JSON output for '{typeof(T).FullName}' is not registered.");
        return JsonSerializer.Serialize(value, (JsonTypeInfo)typeInfo);
    }
}
