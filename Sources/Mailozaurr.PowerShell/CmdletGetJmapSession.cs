namespace Mailozaurr.PowerShell;

/// <summary>Gets the authoritative JMAP Session resource for a saved profile.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPSession")]
[OutputType(typeof(JmapSessionResource))]
public sealed class CmdletGetJmapSession : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() => WriteObject(
        await Application.JmapMailbox.GetSessionAsync(ProfileId!.Trim(), CancelToken).ConfigureAwait(false));
}
