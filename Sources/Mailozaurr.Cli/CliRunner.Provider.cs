using Mailozaurr;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static async Task<int> ExecuteProviderAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error) {
        _ = error;
        if (parseResult.Positionals.Count < 2) {
            await error.WriteLineAsync("Missing provider command. Use 'provider --help' to list provider operations.").ConfigureAwait(false);
            return 1;
        }
        var command = parseResult.Positionals[1];
        var profile = RequireOption(parseResult, "profile");
        var mailbox = parseResult.GetOption("mailbox");
        var json = parseResult.HasFlag("json");
        var resultLimit = Math.Max(1, Math.Min(parseResult.GetIntOption("limit") ?? 100, 999));
        switch (command) {
            case "permission-evidence":
                var evidence = await application.PermissionEvidence.GetEvidenceAsync(profile, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, evidence, json, value => value.Message ?? $"{value.Provider} permission evidence").ConfigureAwait(false);
                return evidence.ProbeSucceeded ? 0 : 1;
            case "graph-rule-list":
                var rules = (await application.GraphMailbox.ListRulesAsync(profile, mailbox, parseResult.GetOption("filter"), resultLimit, parseResult.GetIntOption("max-pages") ?? 25).ConfigureAwait(false))
                    .Take(resultLimit).ToArray();
                await WriteSequenceAsync(output, rules, json, value => value.DisplayName ?? value.Id ?? "(rule)").ConfigureAwait(false);
                return 0;
            case "graph-rule-get":
                var rule = await application.GraphMailbox.GetRuleAsync(profile, RequireOption(parseResult, "rule-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, rule, json, value => value.DisplayName ?? value.Id ?? "(rule)").ConfigureAwait(false);
                return 0;
            case "graph-rule-create":
                var newRule = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GraphInboxRule).ConfigureAwait(false);
                var createdRule = await application.GraphMailbox.CreateRuleAsync(profile, newRule, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, createdRule, json, value => value.DisplayName ?? value.Id ?? "(rule)").ConfigureAwait(false);
                return 0;
            case "graph-rule-update":
                var updatedRuleInput = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GraphInboxRule).ConfigureAwait(false);
                var updatedRule = await application.GraphMailbox.UpdateRuleAsync(profile, RequireOption(parseResult, "rule-id"), updatedRuleInput, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, updatedRule, json, value => value.DisplayName ?? value.Id ?? "(rule)").ConfigureAwait(false);
                return 0;
            case "graph-rule-delete":
                await application.GraphMailbox.DeleteRuleAsync(profile, RequireOption(parseResult, "rule-id"), mailbox).ConfigureAwait(false);
                await output.WriteLineAsync("Graph Inbox rule deleted.").ConfigureAwait(false);
                return 0;
            case "graph-event-list":
                var events = (await application.GraphMailbox.ListEventsAsync(profile, mailbox, parseResult.GetOption("filter"), parseResult.GetOption("select") ?? GraphApiClient.DefaultEventSelect, resultLimit, parseResult.GetIntOption("max-pages") ?? 25).ConfigureAwait(false))
                    .Take(resultLimit).ToArray();
                await WriteSequenceAsync(output, events, json, value => value.Subject ?? value.Id ?? "(event)").ConfigureAwait(false);
                return 0;
            case "graph-event-get":
                var graphEvent = await application.GraphMailbox.GetEventAsync(profile, RequireOption(parseResult, "event-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, graphEvent, json, value => value.Subject ?? value.Id ?? "(event)").ConfigureAwait(false);
                return 0;
            case "graph-event-create":
                var newEvent = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GraphEvent).ConfigureAwait(false);
                var createdEvent = await application.GraphMailbox.CreateEventAsync(profile, newEvent, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, createdEvent, json, value => value.Subject ?? value.Id ?? "(event)").ConfigureAwait(false);
                return 0;
            case "graph-event-update":
                var updateEvent = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GraphEvent).ConfigureAwait(false);
                var updatedEvent = await application.GraphMailbox.UpdateEventAsync(profile, RequireOption(parseResult, "event-id"), updateEvent, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, updatedEvent, json, value => value.Subject ?? value.Id ?? "(event)").ConfigureAwait(false);
                return 0;
            case "graph-event-delete":
                await application.GraphMailbox.DeleteEventAsync(profile, RequireOption(parseResult, "event-id"), mailbox).ConfigureAwait(false);
                await output.WriteLineAsync("Graph event deleted.").ConfigureAwait(false);
                return 0;
            case "graph-thread-get":
                var conversation = (await application.GraphMailbox.GetThreadAsync(profile, RequireOption(parseResult, "thread-id"), mailbox, resultLimit, parseResult.GetIntOption("max-pages") ?? 25).ConfigureAwait(false))
                    .Take(resultLimit).ToArray();
                await WriteSequenceAsync(output, conversation, json, value => value.Subject ?? value.Id).ConfigureAwait(false);
                return 0;
            case "gmail-filter-list":
                var filters = await application.GmailMailbox.ListFiltersAsync(profile, mailbox).ConfigureAwait(false);
                await WriteSequenceAsync(output, filters, json, value => value.Id ?? "(filter)").ConfigureAwait(false);
                return 0;
            case "gmail-filter-get":
                var filter = await application.GmailMailbox.GetFilterAsync(profile, RequireOption(parseResult, "filter-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, filter, json, value => value.Id ?? "(filter)").ConfigureAwait(false);
                return 0;
            case "gmail-filter-create":
                var newFilter = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GmailFilter).ConfigureAwait(false);
                var createdFilter = await application.GmailMailbox.CreateFilterAsync(profile, newFilter, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, createdFilter, json, value => value.Id ?? "(filter)").ConfigureAwait(false);
                return 0;
            case "gmail-filter-delete":
                await application.GmailMailbox.DeleteFilterAsync(profile, RequireOption(parseResult, "filter-id"), mailbox).ConfigureAwait(false);
                await output.WriteLineAsync("Gmail filter deleted.").ConfigureAwait(false);
                return 0;
            case "gmail-label-list":
                var labels = await application.GmailMailbox.ListLabelsAsync(profile, mailbox).ConfigureAwait(false);
                await WriteSequenceAsync(output, labels, json, value => value.Name ?? value.Id ?? "(label)").ConfigureAwait(false);
                return 0;
            case "gmail-label-get":
                var label = await application.GmailMailbox.GetLabelAsync(profile, RequireOption(parseResult, "label-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, label, json, value => value.Name ?? value.Id ?? "(label)").ConfigureAwait(false);
                return 0;
            case "gmail-label-create":
                var newLabel = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GmailLabel).ConfigureAwait(false);
                var createdLabel = await application.GmailMailbox.CreateLabelAsync(profile, newLabel, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, createdLabel, json, value => value.Name ?? value.Id ?? "(label)").ConfigureAwait(false);
                return 0;
            case "gmail-label-update":
                var updateLabel = await ReadJsonFileAsync(RequireOption(parseResult, "file"), CliJsonContext.Default.GmailLabel).ConfigureAwait(false);
                var updatedLabel = await application.GmailMailbox.UpdateLabelAsync(profile, RequireOption(parseResult, "label-id"), updateLabel, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, updatedLabel, json, value => value.Name ?? value.Id ?? "(label)").ConfigureAwait(false);
                return 0;
            case "gmail-label-delete":
                await application.GmailMailbox.DeleteLabelAsync(profile, RequireOption(parseResult, "label-id"), mailbox).ConfigureAwait(false);
                await output.WriteLineAsync("Gmail label deleted.").ConfigureAwait(false);
                return 0;
            case "gmail-thread-list":
                var threads = await application.GmailMailbox.ListThreadsAsync(profile, mailbox, parseResult.GetOption("query"), resultLimit, parseResult.GetOption("cursor")).ConfigureAwait(false);
                await WriteItemAsync(output, threads, json, value => $"{value.Threads.Count} thread(s); cursor={value.NextPageToken ?? "(none)"}").ConfigureAwait(false);
                return 0;
            case "gmail-thread-get":
                var thread = await application.GmailMailbox.GetThreadAsync(profile, RequireOption(parseResult, "thread-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, thread, json, value => value.Id ?? "(thread)").ConfigureAwait(false);
                return 0;
            case "gmail-thread-labels":
                var addLabels = parseResult.GetOptionValues("add-label").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).ToArray();
                var removeLabels = parseResult.GetOptionValues("remove-label").Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).ToArray();
                var modifiedThread = await application.GmailMailbox.ModifyThreadLabelsAsync(profile, RequireOption(parseResult, "thread-id"), addLabels, removeLabels, mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, modifiedThread, json, value => value.Id ?? "(thread)").ConfigureAwait(false);
                return 0;
            case "gmail-thread-trash":
                var trashedThread = await application.GmailMailbox.TrashThreadAsync(profile, RequireOption(parseResult, "thread-id"), mailbox).ConfigureAwait(false);
                await WriteItemAsync(output, trashedThread, json, value => value.Id ?? "(thread)").ConfigureAwait(false);
                return 0;
            case "gmail-thread-delete":
                await application.GmailMailbox.DeleteThreadAsync(profile, RequireOption(parseResult, "thread-id"), mailbox).ConfigureAwait(false);
                await output.WriteLineAsync("Gmail thread permanently deleted.").ConfigureAwait(false);
                return 0;
            default:
                return await WriteUnknownCommandAsync(command, error).ConfigureAwait(false);
        }
    }

    private static async Task<T> ReadJsonFileAsync<T>(string path, JsonTypeInfo<T> typeInfo) {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("JSON file path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path.Trim());
        var json = await File.ReadAllTextAsync(fullPath).ConfigureAwait(false);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new InvalidDataException($"JSON file '{fullPath}' did not contain a {typeof(T).Name} value.");
    }
}
