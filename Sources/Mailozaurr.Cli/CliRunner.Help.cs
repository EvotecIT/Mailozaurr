using Mailozaurr.Application;

namespace Mailozaurr.Cli;

public static partial class CliRunner {
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
