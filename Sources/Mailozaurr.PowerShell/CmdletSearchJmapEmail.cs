namespace Mailozaurr.PowerShell;

/// <summary>Queries a bounded page of JMAP email identifiers.</summary>
[Cmdlet(VerbsCommon.Search, "JMAPEmail")]
[OutputType(typeof(JmapEmailQueryResult))]
public sealed class CmdletSearchJmapEmail : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Restricts results to one mailbox id.</summary>
    [Parameter]
    public string? MailboxId { get; set; }

    /// <summary>Matches server-supported text fields.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <summary>Matches sender text.</summary>
    [Parameter]
    public string? From { get; set; }

    /// <summary>Matches recipient text.</summary>
    [Parameter]
    public string? To { get; set; }

    /// <summary>Matches subject text.</summary>
    [Parameter]
    public string? Subject { get; set; }

    /// <summary>Restricts results to messages received before this timestamp.</summary>
    [Parameter]
    public DateTimeOffset? Before { get; set; }

    /// <summary>Restricts results to messages received after this timestamp.</summary>
    [Parameter]
    public DateTimeOffset? After { get; set; }

    /// <summary>Restricts results by attachment presence.</summary>
    [Parameter]
    public bool? HasAttachment { get; set; }

    /// <summary>Zero-based query position.</summary>
    [Parameter]
    [ValidateRange(0, int.MaxValue)]
    public int Position { get; set; }

    /// <summary>Maximum identifiers returned by this page.</summary>
    [Parameter]
    [ValidateRange(1, 1000)]
    public int Limit { get; set; } = 100;

    /// <summary>Collapses results by JMAP thread when supported.</summary>
    [Parameter]
    public SwitchParameter CollapseThreads { get; set; }

    /// <summary>JMAP property used to order results.</summary>
    [Parameter]
    public string? SortProperty { get; set; }

    /// <summary>Sorts the selected property in descending order.</summary>
    [Parameter]
    public SwitchParameter Descending { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        JmapEmailFilter? filter = HasAnyFilter()
            ? new JmapEmailFilter {
                InMailbox = MailboxId,
                Text = Text,
                From = From,
                To = To,
                Subject = Subject,
                Before = Before,
                After = After,
                HasAttachment = HasAttachment
            }
            : null;
        IReadOnlyList<JmapComparator>? sort = string.IsNullOrWhiteSpace(SortProperty)
            ? null
            : new[] { new JmapComparator { Property = SortProperty!.Trim(), IsAscending = !Descending } };
        JmapEmailQueryResult result = await Application.JmapMailbox.QueryEmailsAsync(
            ProfileId!.Trim(),
            filter,
            sort,
            Position,
            Limit,
            CollapseThreads,
            CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }

    private bool HasAnyFilter() =>
        !string.IsNullOrWhiteSpace(MailboxId) ||
        !string.IsNullOrWhiteSpace(Text) ||
        !string.IsNullOrWhiteSpace(From) ||
        !string.IsNullOrWhiteSpace(To) ||
        !string.IsNullOrWhiteSpace(Subject) ||
        Before.HasValue || After.HasValue || HasAttachment.HasValue;
}
