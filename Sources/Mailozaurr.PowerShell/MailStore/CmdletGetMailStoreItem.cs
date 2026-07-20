using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Enumerates or reads items from an imported mail store.</para>
/// <para type="description">Returns lightweight OfficeIMO.Email item references by default. Use Read to project selected message parts while the owning store remains open.</para>
/// <example>
///   <summary>Enumerate item references</summary>
///   <code>$data | Get-MailStoreItem -MaxItems 100</code>
/// </example>
/// <example>
///   <summary>Read message metadata and bodies</summary>
///   <code>$data | Get-MailStoreItem -Read -Parts Metadata,Bodies -MaxItems 10</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailStoreItem")]
[OutputType(typeof(EmailStoreItemReference), typeof(EmailStoreItem))]
public sealed class CmdletGetMailStoreItem : MailStoreCmdletBase {
    /// <summary>An Import-MailData result containing a store, or a native EmailStoreSession.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [Alias("Store")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Optional stable folder identifier.</summary>
    [Parameter]
    public string? FolderId { get; set; }

    /// <summary>Includes descendants of FolderId.</summary>
    [Parameter]
    public SwitchParameter IncludeDescendants { get; set; }

    /// <summary>Includes folder-associated information items.</summary>
    [Parameter]
    public SwitchParameter IncludeAssociatedItems { get; set; }

    /// <summary>Includes recoverable items absent from normal folder tables.</summary>
    [Parameter]
    public SwitchParameter IncludeOrphanedItems { get; set; }

    /// <summary>Maximum item references returned or read.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int MaxItems { get; set; } = 1_000;

    /// <summary>Reads selected item content instead of returning lightweight references.</summary>
    [Parameter]
    public SwitchParameter Read { get; set; }

    /// <summary>Parts requested when Read is specified.</summary>
    [Parameter]
    public EmailStoreItemReadParts Parts { get; set; } = EmailStoreItemReadParts.All;

    /// <summary>Optional per-item bound for decoded MAPI property bytes.</summary>
    [Parameter]
    [ValidateRange(1L, long.MaxValue)]
    public long? MaxDecodedPropertyBytes { get; set; }

    /// <summary>Prefers reopenable attachment streams over retained byte arrays when supported.</summary>
    [Parameter]
    public SwitchParameter PreferStreamingAttachmentContent { get; set; }

    /// <summary>Enumerates or selectively reads the configured store scope.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            EmailStoreSession session = GetStoreSession(InputObject);
            var enumeration = new EmailStoreEnumerationOptions(
                FolderId,
                IncludeDescendants.IsPresent,
                IncludeAssociatedItems.IsPresent,
                IncludeOrphanedItems.IsPresent,
                MaxItems);
            EmailStoreItemReadOptions? readOptions = Read.IsPresent
                ? new EmailStoreItemReadOptions(
                    Parts,
                    MaxDecodedPropertyBytes,
                    PreferStreamingAttachmentContent.IsPresent)
                : null;
            foreach (EmailStoreItemReference reference in session.EnumerateItems(enumeration, CancelToken)) {
                WriteObject(readOptions == null
                    ? reference
                    : session.ReadItem(reference, readOptions, CancelToken));
            }
        } catch (OperationCanceledException) when (CancelToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreItemReadFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }
}
