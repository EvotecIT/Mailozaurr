namespace Mailozaurr.PowerShell;

/// <summary>Gets a bounded set of JMAP email objects by id.</summary>
[Cmdlet(VerbsCommon.Get, "JMAPEmail")]
[OutputType(typeof(JmapEmailGetResult))]
public sealed class CmdletGetJmapEmail : MailApplicationCmdletBase {
    /// <summary>JMAP profile identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Id")]
    public string? ProfileId { get; set; }

    /// <summary>Email identifiers to retrieve.</summary>
    [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public string[]? EmailId { get; set; }

    /// <summary>Optional JMAP Email properties to request.</summary>
    [Parameter]
    public string[]? Property { get; set; }

    /// <inheritdoc />
    protected override async Task ProcessRecordAsync() => WriteObject(
        await Application.JmapMailbox.GetEmailsAsync(
            ProfileId!.Trim(),
            EmailId!,
            Property,
            CancelToken).ConfigureAwait(false));
}
