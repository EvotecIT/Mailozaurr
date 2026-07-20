using OfficeIMO.Email;
using OfficeIMO.Email.AddressBook;
using OfficeIMO.Email.Data;
using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Opens an email-data artifact through its OfficeIMO.Email owner.</para>
/// <para type="description">Detects EML, MSG, OFT, TNEF, ICS, VCF, PST, OST, OLM, EMLX, Mbox, Maildir, Apple Mail directories, and Outlook Offline Address Book data. The returned owner result must be closed when it contains a store, address-book session, or streaming email content.</para>
/// <example>
///   <summary>Open a PST and browse its folders</summary>
///   <code>$data = Import-MailData './archive.pst'
/// try { $data | Get-MailStoreFolder } finally { $data | Close-MailData }</code>
/// </example>
/// <example>
///   <summary>Open an iCalendar artifact</summary>
///   <code>$data = Import-MailData './meeting.ics'
/// $data.Calendar.GetComponents('VEVENT')</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.Import, "MailData")]
[OutputType(typeof(EmailDataOpenResult))]
public sealed class CmdletImportMailData : MailStoreCmdletBase {
    /// <summary>Path to one supported email-data file or mailbox directory.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
    [Alias("Path", "FullName")]
    [ValidateNotNullOrEmpty]
    public string? InputPath { get; set; }

    /// <summary>Optional expected owner for ambiguous or extension-free input.</summary>
    [Parameter]
    public EmailDataArtifactKind? ExpectedKind { get; set; }

    /// <summary>Uses file-backed streaming content for individual email artifacts.</summary>
    [Parameter]
    public SwitchParameter UseStreamingEmailReader { get; set; }

    /// <summary>Optional bounded policy for individual EML, MSG, OFT, or TNEF artifacts.</summary>
    [Parameter]
    public EmailReaderOptions? EmailReaderOptions { get; set; }

    /// <summary>Optional bounded policy for ICS and VCF content-line artifacts.</summary>
    [Parameter]
    public ContentLineReaderOptions? ContentLineReaderOptions { get; set; }

    /// <summary>Optional bounded policy for PST, OST, OLM, EMLX, Mbox, and mailbox-directory stores.</summary>
    [Parameter]
    public EmailStoreReaderOptions? StoreReaderOptions { get; set; }

    /// <summary>Optional bounded policy for Outlook Offline Address Book artifacts.</summary>
    [Parameter]
    public OfflineAddressBookReaderOptions? AddressBookReaderOptions { get; set; }

    /// <summary>Opens the selected artifact and transfers ownership to the pipeline.</summary>
    protected override Task ProcessRecordAsync() {
        string? inputPath = InputPath;
        if (string.IsNullOrWhiteSpace(inputPath)) return Task.CompletedTask;

        EmailDataOpenResult? result = null;
        try {
            string path = GetUnresolvedProviderPathFromPSPath(inputPath);
            var options = new EmailDataOpenOptions(
                EmailReaderOptions,
                ContentLineReaderOptions,
                StoreReaderOptions,
                AddressBookReaderOptions,
                ExpectedKind,
                UseStreamingEmailReader.IsPresent);
            result = EmailDataArtifact.Open(path, options, CancelToken);
            WriteObject(result);
            result = null;
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (FileNotFoundException exception) {
            WriteError(new ErrorRecord(exception, "MailDataNotFound", ErrorCategory.ObjectNotFound, inputPath));
        } catch (NotSupportedException exception) {
            WriteError(new ErrorRecord(exception, "MailDataTypeNotSupported", ErrorCategory.InvalidType, inputPath));
        } catch (InvalidDataException exception) {
            WriteError(new ErrorRecord(exception, "MailDataInvalid", ErrorCategory.InvalidData, inputPath));
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailDataImportFailed", ErrorCategory.ReadError, inputPath));
        } finally {
            result?.Dispose();
        }
        return Task.CompletedTask;
    }
}
