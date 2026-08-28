namespace Mailozaurr.PowerShell;

/// <summary>Gets bounded JMAP email changes since an opaque state token.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPEmailChange")]
[OutputType(typeof(JmapEmailChangesResult))]
public sealed class CmdletGetJmapEmailChange : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Opaque prior Email state token.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public string? SinceState { get; set; }

    /// <summary>Maximum changes returned by the server.</summary>
    [Parameter]
    [ValidateRange(1, 100000)]
    public int MaxChanges { get; set; } = 1000;

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() => WriteObject(
        await Application.JmapMailbox.GetEmailChangesAsync(
            ProfileId!.Trim(),
            SinceState!.Trim(),
            MaxChanges,
            CancelToken).ConfigureAwait(false));
}
