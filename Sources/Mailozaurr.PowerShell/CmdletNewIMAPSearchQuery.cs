using MailKit.Search;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates a Mailozaurr IMAP search query without requiring callers to construct MailKit types directly.
/// </summary>
[Cmdlet(VerbsCommon.New, "IMAPSearchQuery")]
[OutputType(typeof(SearchQuery))]
public sealed class CmdletNewIMAPSearchQuery : PSCmdlet {
    /// <summary>Filters messages by subject text.</summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>Filters messages by sender text.</summary>
    [Parameter]
    public string? FromContains { get; set; }

    /// <summary>Filters messages by recipient text.</summary>
    [Parameter]
    public string? ToContains { get; set; }

    /// <summary>Filters messages by body text.</summary>
    [Parameter]
    public string? BodyContains { get; set; }

    /// <summary>Filters messages where any searchable message content contains this text.</summary>
    [Parameter]
    public string[]? MessageContains { get; set; }

    /// <summary>Header name to search.</summary>
    [Parameter]
    public string? HeaderName { get; set; }

    /// <summary>Header value to search.</summary>
    [Parameter]
    public string? HeaderValue { get; set; }

    /// <summary>Only messages delivered after this date are matched.</summary>
    [Parameter]
    public DateTime? Since { get; set; }

    /// <summary>Only messages delivered before this date are matched.</summary>
    [Parameter]
    public DateTime? Before { get; set; }

    /// <summary>Only unread messages are matched.</summary>
    [Parameter]
    public SwitchParameter Unseen { get; set; }

    /// <summary>Only read messages are matched.</summary>
    [Parameter]
    public SwitchParameter Seen { get; set; }

    /// <summary>Only answered messages are matched.</summary>
    [Parameter]
    public SwitchParameter Answered { get; set; }

    /// <summary>Only unanswered messages are matched.</summary>
    [Parameter]
    public SwitchParameter Unanswered { get; set; }

    /// <summary>Only flagged messages are matched.</summary>
    [Parameter]
    public SwitchParameter Flagged { get; set; }

    /// <summary>Only unflagged messages are matched.</summary>
    [Parameter]
    public SwitchParameter Unflagged { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        SearchQuery query = SearchQuery.All;

        if (!string.IsNullOrWhiteSpace(Subject)) {
            query = query.And(SearchQuery.SubjectContains(Subject!));
        }

        if (!string.IsNullOrWhiteSpace(FromContains)) {
            query = query.And(SearchQuery.FromContains(FromContains!));
        }

        if (!string.IsNullOrWhiteSpace(ToContains)) {
            query = query.And(SearchQuery.ToContains(ToContains!));
        }

        if (!string.IsNullOrWhiteSpace(BodyContains)) {
            query = query.And(SearchQuery.BodyContains(BodyContains!));
        }

        if (Since.HasValue) {
            query = query.And(SearchQuery.DeliveredAfter(Since.Value));
        }

        if (Before.HasValue) {
            query = query.And(SearchQuery.DeliveredBefore(Before.Value));
        }

        if (Unseen.IsPresent) {
            query = query.And(SearchQuery.NotSeen);
        }

        if (Seen.IsPresent) {
            query = query.And(SearchQuery.Seen);
        }

        if (Answered.IsPresent) {
            query = query.And(SearchQuery.Answered);
        }

        if (Unanswered.IsPresent) {
            query = query.And(SearchQuery.NotAnswered);
        }

        if (Flagged.IsPresent) {
            query = query.And(SearchQuery.Flagged);
        }

        if (Unflagged.IsPresent) {
            query = query.And(SearchQuery.NotFlagged);
        }

        if (!string.IsNullOrWhiteSpace(HeaderName) || !string.IsNullOrWhiteSpace(HeaderValue)) {
            if (string.IsNullOrWhiteSpace(HeaderName) || string.IsNullOrWhiteSpace(HeaderValue)) {
                ThrowTerminatingError(new ErrorRecord(
                    new PSArgumentException("HeaderName and HeaderValue must be provided together."),
                    "IncompleteHeaderFilter",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            query = query.And(SearchQuery.HeaderContains(HeaderName!, HeaderValue!));
        }

        if (MessageContains != null) {
            foreach (var value in MessageContains) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    query = query.And(SearchQuery.MessageContains(value));
                }
            }
        }

        WriteObject(query);
    }
}
