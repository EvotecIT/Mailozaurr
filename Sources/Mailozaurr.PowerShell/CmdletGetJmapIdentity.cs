namespace Mailozaurr.PowerShell;

/// <summary>Lists JMAP sending identities when submission is available.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPIdentity")]
[OutputType(typeof(JmapIdentity))]
public sealed class CmdletGetJmapIdentity : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() {
        IReadOnlyList<JmapIdentity> identities = await Application.JmapMailbox
            .ListIdentitiesAsync(ProfileId!.Trim(), CancelToken).ConfigureAwait(false);
        foreach (JmapIdentity identity in identities) WriteObject(identity);
    }
}
