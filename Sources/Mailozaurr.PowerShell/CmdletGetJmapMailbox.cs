namespace Mailozaurr.PowerShell;

/// <summary>Lists JMAP mailboxes and effective rights for a saved profile.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPMailbox")]
[OutputType(typeof(JmapMailbox))]
public sealed class CmdletGetJmapMailbox : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        IReadOnlyList<JmapMailbox> mailboxes = await Application.JmapMailbox
            .ListMailboxesAsync(ProfileId!.Trim(), CancelToken).ConfigureAwait(false);
        foreach (JmapMailbox mailbox in mailboxes) WriteObject(mailbox);
    }
}
