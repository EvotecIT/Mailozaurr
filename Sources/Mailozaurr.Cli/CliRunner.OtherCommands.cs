using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static async Task<int> ExecuteDraftAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing draft command. Use 'draft list', 'draft save', 'draft get', 'draft delete', or 'draft export'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("compact")) {
                    var compactDrafts = await application.Drafts.GetDraftsCompactAsync().ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactDrafts, json, draft =>
                        draft.Summary).ConfigureAwait(false);
                    return 0;
                }
                var drafts = await application.Drafts.GetDraftsAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, drafts, json, draft =>
                    $"{draft.Id} [{draft.Message.ProfileId}] {draft.Name}").ConfigureAwait(false);
                return 0;
            case "save":
                var draft = await BuildMailDraftAsync(application, parseResult).ConfigureAwait(false);
                var saveResult = await application.Drafts.SaveAsync(draft).ConfigureAwait(false);
                await WriteItemAsync(output, saveResult, json, value => value.Message ?? "Draft saved.").ConfigureAwait(false);
                return saveResult.Succeeded ? 0 : 1;
            case "get":
                if (parseResult.HasFlag("compact")) {
                    var compactStoredDraft = await application.Drafts.GetDraftCompactAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                    if (compactStoredDraft == null) {
                        await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactStoredDraft, json, value => value.Summary).ConfigureAwait(false);
                    return 0;
                }
                var storedDraft = await application.Drafts.GetDraftAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                if (storedDraft == null) {
                    await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, storedDraft, json, value => $"{value.Id} [{value.Message.ProfileId}] {value.Name}").ConfigureAwait(false);
                return 0;
            case "delete":
                var deleteResult = await application.Drafts.DeleteAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                await WriteItemAsync(output, deleteResult, json, value => value.Message ?? "Draft deleted.").ConfigureAwait(false);
                return deleteResult.Succeeded ? 0 : 1;
            case "export":
                var draftToExport = await application.Drafts.GetDraftAsync(RequireOption(parseResult, "draft")).ConfigureAwait(false);
                if (draftToExport == null) {
                    await error.WriteLineAsync("Draft was not found.").ConfigureAwait(false);
                    return 1;
                }
                await application.DraftExchange.SaveAsync(RequireOption(parseResult, "path"), draftToExport).ConfigureAwait(false);
                await WriteItemAsync(output, OperationResult.Success("Draft exported."), json, value => value.Message ?? "Draft exported.").ConfigureAwait(false);
                return 0;
            default:
                await error.WriteLineAsync($"Unknown draft command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteQueueAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing queue command. Use 'queue list', 'queue get', 'queue remove', 'queue process', 'queue dead-letter-list', 'queue dead-letter-get', or 'queue dead-letter-remove'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        var json = parseResult.HasFlag("json");
        switch (subCommand) {
            case "list":
                if (parseResult.HasFlag("compact")) {
                    var compactQueuedMessages = await application.Queue.ListCompactAsync().ConfigureAwait(false);
                    await WriteSequenceAsync(output, compactQueuedMessages, json, message => message.Summary).ConfigureAwait(false);
                    return 0;
                }
                var queuedMessages = await application.Queue.ListAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, queuedMessages, json, message =>
                    $"{message.MessageId} [{message.Provider}] attempts={message.AttemptCount} next={message.NextAttemptAt:O}").ConfigureAwait(false);
                return 0;
            case "get":
                if (parseResult.HasFlag("compact")) {
                    var compactQueuedMessage = await application.Queue.GetCompactAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                    if (compactQueuedMessage == null) {
                        await error.WriteLineAsync("Queued message was not found.").ConfigureAwait(false);
                        return 1;
                    }
                    await WriteItemAsync(output, compactQueuedMessage, json, message => message.Summary).ConfigureAwait(false);
                    return 0;
                }
                var queuedMessage = await application.Queue.GetAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                if (queuedMessage == null) {
                    await error.WriteLineAsync("Queued message was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, queuedMessage, json, message =>
                    $"{message.MessageId} [{message.Provider}] attempts={message.AttemptCount}").ConfigureAwait(false);
                return 0;
            case "remove":
                var removeResult = await application.Queue.RemoveAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                await WriteItemAsync(output, removeResult, json, value => value.Message ?? "Queued message removed.").ConfigureAwait(false);
                return removeResult.Succeeded ? 0 : 1;
            case "dead-letter-list":
                var deadLetters = await application.Queue.ListDeadLettersAsync().ConfigureAwait(false);
                await WriteSequenceAsync(output, deadLetters, json, message =>
                    $"{message.MessageId} [{message.Provider}] reason={message.DeadLetterReason} error={message.ErrorMessage ?? "(none)"}").ConfigureAwait(false);
                return 0;
            case "dead-letter-get":
                var deadLetter = await application.Queue.GetDeadLetterAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                if (deadLetter == null) {
                    await error.WriteLineAsync("Dead-lettered message was not found.").ConfigureAwait(false);
                    return 1;
                }
                await WriteItemAsync(output, deadLetter, json, message =>
                    $"{message.MessageId} [{message.Provider}] reason={message.DeadLetterReason}").ConfigureAwait(false);
                return 0;
            case "dead-letter-remove":
                var removeDeadLetterResult = await application.Queue.RemoveDeadLetterAsync(RequireOption(parseResult, "message-id")).ConfigureAwait(false);
                await WriteItemAsync(output, removeDeadLetterResult, json, value => value.Message ?? "Dead-lettered message removed.").ConfigureAwait(false);
                return removeDeadLetterResult.Succeeded ? 0 : 1;
            case "process":
                var processResult = await application.Queue.ProcessAsync().ConfigureAwait(false);
                await WriteItemAsync(output, processResult, json, value =>
                    value.Message ?? $"Processed queue: attempted={value.AttemptedCount}, sent={value.SentCount}, failed={value.FailedCount}.").ConfigureAwait(false);
                return processResult.Succeeded ? 0 : 1;
            default:
                await error.WriteLineAsync($"Unknown queue command '{subCommand}'.").ConfigureAwait(false);
                return 1;
        }
    }

    private static async Task<int> ExecuteSendAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        var json = parseResult.HasFlag("json");
        var request = await BuildSendRequestAsync(application, parseResult).ConfigureAwait(false);
        var result = await application.Send.SendAsync(request).ConfigureAwait(false);
        await WriteItemAsync(output, result, json, value => value.Message ?? (value.Queued
            ? $"Message queued: {value.QueueMessageId ?? "(unknown-id)"}"
            : $"Message sent: {value.ProviderMessageId ?? "(provider-id unavailable)"}")).ConfigureAwait(false);
        return result.Succeeded ? 0 : 1;
    }

    private static async Task<int> ExecuteMcpAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter error) {
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing mcp command. Use 'mcp serve'.").ConfigureAwait(false);
            return 1;
        }

        var subCommand = parseResult.Positionals[1];
        if (!string.Equals(subCommand, "serve", StringComparison.OrdinalIgnoreCase)) {
            await error.WriteLineAsync($"Unknown mcp command '{subCommand}'.").ConfigureAwait(false);
            return 1;
        }

        await McpServerHost.RunAsync(application).ConfigureAwait(false);
        return 0;
    }
}
