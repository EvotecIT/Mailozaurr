using Mailozaurr.Hosting;
using Mailozaurr.Cli.Mcp;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        Func<MailApplicationOptions, MailApplicationBuilder>? builderFactory = null,
        TextReader? input = null) {
        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }
        if (output == null) {
            throw new ArgumentNullException(nameof(output));
        }
        if (error == null) {
            throw new ArgumentNullException(nameof(error));
        }

        CliArguments parseResult;
        try {
            parseResult = CliArguments.Parse(args);
        } catch (Exception ex) {
            await WriteExceptionAsync(
                error,
                ex,
                args.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase)))
                .ConfigureAwait(false);
            return 1;
        }
        input ??= TextReader.Null;
        if (parseResult.ShowHelp || parseResult.Positionals.Count == 0) {
            parseResult.WriteHelp(output);
            return 0;
        }

        var options = new MailApplicationOptions();
        var profilesDir = parseResult.GetOption("profiles-dir");
        if (!string.IsNullOrWhiteSpace(profilesDir)) {
            options.ProfileStore.DirectoryPath = profilesDir;
        }
        var secretsDir = parseResult.GetOption("secrets-dir");
        if (!string.IsNullOrWhiteSpace(secretsDir)) {
            options.SecretStore.DirectoryPath = secretsDir;
        }
        var draftsDir = parseResult.GetOption("drafts-dir");
        if (!string.IsNullOrWhiteSpace(draftsDir)) {
            options.DraftStore.DirectoryPath = draftsDir;
        }
        var planBatchesDir = parseResult.GetOption("plan-batches-dir");
        if (!string.IsNullOrWhiteSpace(planBatchesDir)) {
            options.ActionPlanBatchStore.DirectoryPath = planBatchesDir;
        }

        var application = (builderFactory ?? (appOptions => new MailApplicationBuilder(appOptions)))
            .Invoke(options)
            .Build();

        try {
            return await ExecuteAsync(application, parseResult, output, error, input).ConfigureAwait(false);
        } catch (Exception ex) {
            await WriteExceptionAsync(error, ex, parseResult.HasFlag("json")).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> ExecuteAsync(
        MailApplication application,
        CliArguments parseResult,
        TextWriter output,
        TextWriter error,
        TextReader input) {
        var command = parseResult.Positionals[0];
        return command switch {
            "profile" => await ExecuteProfileAsync(application, parseResult, output, error, input).ConfigureAwait(false),
            "draft" => await ExecuteDraftAsync(application, parseResult, output, error).ConfigureAwait(false),
            "mail" => await ExecuteMailAsync(application, parseResult, output, error).ConfigureAwait(false),
            "mcp" => await ExecuteMcpAsync(application, parseResult, error).ConfigureAwait(false),
            "send" => await ExecuteSendAsync(application, parseResult, output, error).ConfigureAwait(false),
            "queue" => await ExecuteQueueAsync(application, parseResult, output, error).ConfigureAwait(false),
            _ => await WriteUnknownCommandAsync(command, error).ConfigureAwait(false)
        };
    }






}
