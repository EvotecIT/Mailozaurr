using Mailozaurr.Application;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    private static void WriteHelp(TextWriter output) {
        output.WriteLine("Mailozaurr CLI");
        output.WriteLine();
        output.WriteLine("Commands:");
        output.WriteLine("  profile list [--summary] [--compact] [--kind <kind>] [--ready-only] [--can-read] [--can-send] [--default-only] [--sort <id|kind|readiness>] [--desc] [--json]");
        output.WriteLine("  profile create --profile <id> --kind <kind> --name <display-name> [--description <text>] [--default-sender <email>] [--default-mailbox <value>] [--is-default] [--setting <key=value>] [--json]");
        output.WriteLine("  profile graph-bootstrap --profile <id> --name <display-name> --mailbox <address> [--description <text>] [--default-sender <email>] [--is-default] [--client-id <id>] [--tenant-id <id>] [--client-secret-env <name>|--client-secret-stdin|--client-secret-ref <profile-id:secret-name>] [--access-token-env <name>|--access-token-stdin|--access-token-ref <profile-id:secret-name>] [--certificate-path <path>] [--certificate-password-env <name>|--certificate-password-stdin|--certificate-password-ref <profile-id:secret-name>] [--json]");
        output.WriteLine("  profile gmail-bootstrap --profile <id> --name <display-name> [--mailbox <address|me>] [--description <text>] [--default-sender <email>] [--is-default] [--client-id <id>] [--client-secret-env <name>|--client-secret-stdin|--client-secret-ref <profile-id:secret-name>] [--refresh-token-env <name>|--refresh-token-stdin|--refresh-token-ref <profile-id:secret-name>] [--access-token-env <name>|--access-token-stdin|--access-token-ref <profile-id:secret-name>] [--json]");
        output.WriteLine("  profile graph-login --profile <id> [--login <upn>] [--mailbox <address>] [--client-id <id>] [--tenant-id <id>] [--redirect-uri <uri>] [--scope <value>] [--scope <value>] [--json]");
        output.WriteLine("  profile gmail-login --profile <id> [--mailbox <address>] [--client-id <id>] [--client-secret-env <name>|--client-secret-stdin|--client-secret-ref <profile-id:secret-name>] [--scope <value>] [--scope <value>] [--json]");
        output.WriteLine("  profile refresh-auth --profile <id> [--json]");
        output.WriteLine("  profile auth-status --profile <id> [--json]");
        output.WriteLine("  profile test --profile <id> [--scope <auto|auth|mailbox|send>] [--json]");
        output.WriteLine("  profile summary --profile <id> [--compact] [--json]");
        output.WriteLine("  profile capabilities --profile <id> [--json]");
        output.WriteLine("  profile show --profile <id> [--json]");
        output.WriteLine("  profile validate --profile <id> [--json]");
        output.WriteLine("  profile doctor --profile <id> [--json]");
        output.WriteLine("  profile delete --profile <id> [--json]");
        output.WriteLine("  profile set-default --profile <id> [--json]");
        output.WriteLine("  profile set-secret --profile <id> --name <secret-name> [--value-env <name>|--value-stdin|--value-ref <profile-id:secret-name>] [--json]");
        output.WriteLine("  profile remove-secret --profile <id> --name <secret-name> [--json]");
        output.WriteLine("  profile inspect-orphan-secrets [--json]");
        output.WriteLine("  profile cleanup-orphan-secrets [--json]");
        output.WriteLine("  draft list [--compact] [--json]");
        output.WriteLine("  draft save --file <path> [--draft <id>] [--name <display-name>] [--json]");
        output.WriteLine("  draft save --draft <id> --name <display-name> --profile <id> --to <address> [--to <address>] [--cc <address>] [--bcc <address>] [--reply-to <address>] [--from <address>] [--subject <text>] [--text <text>] [--html <html>] [--attachment <path>] [--header <key=value>] [--json]");
        output.WriteLine("  draft get --draft <id> [--compact] [--json]");
        output.WriteLine("  draft delete --draft <id> [--json]");
        output.WriteLine("  draft export --draft <id> --path <file> [--json]");
        output.WriteLine("  mail folders --profile <id> [--mailbox <id>] [--parent-folder <id>] [--root-only] [--compact] [--json]");
        output.WriteLine("  mail folder-aliases --profile <id> [--mailbox <id>] [--json]");
        output.WriteLine("  mail resolve-folder --profile <id> --target-folder <id> [--mailbox <id>] [--json]");
        output.WriteLine("  mail list-plan-batches [--summary|--compact] [--plan-name <name>] [--plan-name <name>] [--profile <id>] [--profile <id>] [--action <name>] [--action <name>] [--sort <id|name|plans|ready|updated|actions>] [--desc] [--json]");
        output.WriteLine("  mail show-plan-batch --batch <id> [--summary|--compact] [--json]");
        output.WriteLine("  mail import-plan-batch --batch <id> --name <display-name> --path <file> [--description <text>] [--json]");
        output.WriteLine("  mail export-plan-batch --batch <id> --path <file> [--json]");
        output.WriteLine("  mail create-common-plan-batch --batch <id> --name <display-name> --profile <id> --message-id <id> [--message-id <id>] [--action <name>] [--action <name>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--description <text>] [--json]");
        output.WriteLine("  mail clone-plan-batch --source-batch <id> --target-batch <id> --name <display-name> [--description <text>] [--json]");
        output.WriteLine("  mail preview-transform-plan-batch --source-batch <id> [--index <n>] [--index <n>] [--plan-name <name>] [--plan-name <name>] [--target-profile <id>] [--mailbox <id>] [--folder <name>] [--target-folder <id>] [--json]");
        output.WriteLine("  mail transform-plan-batch --source-batch <id> --target-batch <id> --name <display-name> [--index <n>] [--index <n>] [--plan-name <name>] [--plan-name <name>] [--target-profile <id>] [--mailbox <id>] [--folder <name>] [--target-folder <id>] [--description <text>] [--json]");
        output.WriteLine("  mail add-plan-to-batch --batch <id> --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail add-plan-file-to-batch --batch <id> --path <file> [--json]");
        output.WriteLine("  mail replace-plan-in-batch --batch <id> --index <n> --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail replace-plan-file-in-batch --batch <id> --index <n> --path <file> [--json]");
        output.WriteLine("  mail remove-plan-from-batch --batch <id> --index <n> [--json]");
        output.WriteLine("  mail delete-plan-batch --batch <id> [--json]");
        output.WriteLine("  mail execute-plan-batch-stored --batch <id> [--confirm-token <token>] [--stop-on-error] [--json]");
        output.WriteLine("  mail plan-action --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail export-plan --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] --path <file> [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail show-plan --path <file> [--json]");
        output.WriteLine("  mail execute-plan --action <mark-read|mark-unread|flag|unflag|archive|trash|move|delete> --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail execute-plan-file --path <file> [--json]");
        output.WriteLine("  mail execute-plan-batch --path <file> [--stop-on-error] [--json]");
        output.WriteLine("  mail preview-all --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-mark-read --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unread] [--json]");
        output.WriteLine("  mail preview-flag --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unflag] [--json]");
        output.WriteLine("  mail preview-actions --profile <id> --message-id <id> [--message-id <id>] [--target-folder <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-move --profile <id> --message-id <id> [--message-id <id>] --target-folder <id> [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail preview-delete --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail search --profile <id> [--mailbox <id>] [--folder <name>] [--query <text>] [--subject <text>] [--from <text>] [--to <text>] [--limit <n>] [--compact] [--json]");
        output.WriteLine("  mail get --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--include-raw] [--compact] [--json]");
        output.WriteLine("  mail get-many --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--include-raw] [--compact] [--json]");
        output.WriteLine("  mail mark-read --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unread] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail flag --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--unflag] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail archive --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail trash --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail move --profile <id> --message-id <id> [--message-id <id>] --target-folder <id> [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail delete --profile <id> --message-id <id> [--message-id <id>] [--mailbox <id>] [--folder <name>] [--confirm-token <token>] [--json]");
        output.WriteLine("  mail attachments --profile <id> --message-id <id> [--mailbox <id>] [--folder <name>] [--json]");
        output.WriteLine("  mail save-attachment --profile <id> --message-id <id> --attachment-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--overwrite] [--json]");
        output.WriteLine("  mail save-attachments --profile <id> --message-id <id> --path <destination> [--mailbox <id>] [--folder <name>] [--attachment-id <id>] [--attachment-id <id>] [--name-contains <text>] [--content-type <text>] [--overwrite] [--json]");
        output.WriteLine("  mail save-attachments-many --profile <id> --message-id <id> [--message-id <id>] --path <destination> [--mailbox <id>] [--folder <name>] [--attachment-id <id>] [--attachment-id <id>] [--name-contains <text>] [--content-type <text>] [--overwrite] [--json]");
        output.WriteLine("  mcp serve");
        output.WriteLine("  send --draft <id> [--queue-on-failure] [--json]");
        output.WriteLine("  send --file <path> [--queue-on-failure] [--json]");
        output.WriteLine("  send --profile <id> --to <address> [--to <address>] [--cc <address>] [--bcc <address>] [--reply-to <address>] [--from <address>] [--subject <text>] [--text <text>] [--html <html>] [--attachment <path>] [--header <key=value>] [--queue-on-failure] [--json]");
        output.WriteLine("  queue list [--compact] [--json]");
        output.WriteLine("  queue get --message-id <id> [--compact] [--json]");
        output.WriteLine("  queue remove --message-id <id> [--json]");
        output.WriteLine("  queue process [--json]");
        output.WriteLine("  queue dead-letter-list [--json]");
        output.WriteLine("  queue dead-letter-get --message-id <id> [--json]");
        output.WriteLine("  queue dead-letter-remove --message-id <id> [--json]");
        output.WriteLine();
        output.WriteLine("Global options:");
        output.WriteLine("  --profiles-dir <path>");
        output.WriteLine("  --secrets-dir <path>");
        output.WriteLine("  --drafts-dir <path>");
        output.WriteLine("  --plan-batches-dir <path>");
        output.WriteLine("  --help");
    }

    private static MailProfileConnectionTestScope ParseConnectionTestScope(string? rawScope) {
        if (string.IsNullOrWhiteSpace(rawScope)) {
            return MailProfileConnectionTestScope.Auto;
        }

        if (Enum.TryParse<MailProfileConnectionTestScope>(rawScope.Trim(), ignoreCase: true, out var scope)) {
            return scope;
        }

        throw new InvalidOperationException($"Unsupported connection test scope '{rawScope}'.");
    }

    private static MailProfileOverviewQuery BuildProfileOverviewQuery(CliArguments parseResult) {
        var query = new MailProfileOverviewQuery {
            Descending = parseResult.HasFlag("desc"),
            ReadyOnly = parseResult.HasFlag("ready-only"),
            CanReadOnly = parseResult.HasFlag("can-read"),
            CanSendOnly = parseResult.HasFlag("can-send"),
            DefaultOnly = parseResult.HasFlag("default-only")
        };

        var kind = parseResult.GetOption("kind");
        if (!string.IsNullOrWhiteSpace(kind)) {
            query.Kind = MailProfileKindParser.Parse(kind);
        }

        var sort = parseResult.GetOption("sort");
        if (!string.IsNullOrWhiteSpace(sort)) {
            query.SortBy = ParseProfileOverviewSortBy(sort);
        }

        return query;
    }

    private static MailProfileOverviewSortBy ParseProfileOverviewSortBy(string rawSort) {
        if (Enum.TryParse<MailProfileOverviewSortBy>(rawSort.Trim(), ignoreCase: true, out var sortBy)) {
            return sortBy;
        }

        throw new InvalidOperationException($"Unsupported profile overview sort '{rawSort}'.");
    }
}