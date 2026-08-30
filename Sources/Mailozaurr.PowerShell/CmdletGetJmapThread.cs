namespace Mailozaurr.PowerShell;

/// <summary>Gets JMAP threads by id.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPThread")]
[OutputType(typeof(JmapThread))]
public sealed class CmdletGetJmapThread : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Thread identifiers.</summary>
    [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string[]? ThreadId { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        IReadOnlyList<JmapThread> threads = await Application.JmapMailbox
            .GetThreadsAsync(ProfileId!.Trim(), ThreadId!, CancelToken).ConfigureAwait(false);
        foreach (JmapThread thread in threads) WriteObject(thread);
    }
}
