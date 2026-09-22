using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Compares two email documents by their semantic content.</para>
/// <para type="description">Returns the native OfficeIMO.Email difference report. Import both artifacts first and keep their results open until comparison finishes.</para>
/// <example>
///   <summary>Verify two EML files</summary>
///   <code>$source = Import-MailData './source.eml' -UseStreamingEmailReader
/// $copy = Import-MailData './copy.eml' -UseStreamingEmailReader
/// try { $source | Compare-MailDataSemantic -ReferenceObject $copy }
/// finally { $source, $copy | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.Compare, "MailDataSemantic")]
[OutputType(typeof(EmailSemanticComparisonReport))]
public sealed class CmdletCompareMailDataSemantic : MailStoreCmdletBase {
    /// <summary>Source email or Import-MailData result.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Destination email or Import-MailData result.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNull]
    public object? ReferenceObject { get; set; }

    /// <summary>Optional OfficeIMO semantic comparison policy.</summary>
    [Parameter]
    public EmailSemanticComparisonOptions? Options { get; set; }

    /// <summary>Produces the native privacy-safe comparison report.</summary>
    protected override async Task ProcessRecordAsync() {
        try {
            var source = GetEmailDocument(InputObject);
            var destination = GetEmailDocument(ReferenceObject);
            WriteObject(await EmailSemanticComparer.CompareAsync(source, destination, Options, CancelToken)
                .ConfigureAwait(false));
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailSemanticComparisonFailed", ErrorCategory.InvalidData, InputObject));
        }
    }
}
