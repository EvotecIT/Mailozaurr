using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Computes a semantic fingerprint of an email document.</para>
/// <para type="description">Uses OfficeIMO.Email to fingerprint normalized content and attachments. Supply keyed digest options before persisting fingerprints of private mail.</para>
/// <example>
///   <summary>Find repeated messages</summary>
///   <code>$email = Import-MailData './message.eml' -UseStreamingEmailReader
/// try { $email | Get-MailSemanticFingerprint } finally { $email | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailSemanticFingerprint")]
[OutputType(typeof(EmailSemanticFingerprint))]
public sealed class CmdletGetMailSemanticFingerprint : MailStoreCmdletBase {
    /// <summary>Email or Import-MailData result.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Optional OfficeIMO semantic comparison policy.</summary>
    [Parameter]
    public EmailSemanticComparisonOptions? Options { get; set; }

    /// <summary>Produces the native semantic fingerprint.</summary>
    protected override async Task ProcessRecordAsync() {
        try {
            var document = GetEmailDocument(InputObject);
            WriteObject(await EmailSemanticComparer.CreateFingerprintAsync(document, Options, CancelToken)
                .ConfigureAwait(false));
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailSemanticFingerprintFailed", ErrorCategory.InvalidData, InputObject));
        }
    }
}
