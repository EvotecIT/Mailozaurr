using OfficeIMO.Email.Store;

namespace Mailozaurr.PowerShell;

/// <summary>
/// <para type="synopsis">Lists folders from an imported PST, OST, OLM, Mbox, EMLX, or mailbox directory.</para>
/// <para type="description">Returns OfficeIMO.Email folder metadata without decoding message bodies or attachments.</para>
/// <example>
///   <summary>List archive folders</summary>
///   <code>$data = Import-MailData './archive.ost'
/// try { $data | Get-MailStoreFolder } finally { $data | Close-MailData }</code>
/// </example>
/// </summary>
[Cmdlet(VerbsCommon.Get, "MailStoreFolder")]
[OutputType(typeof(EmailStoreFolderInfo))]
public sealed class CmdletGetMailStoreFolder : MailStoreCmdletBase {
    /// <summary>An Import-MailData result containing a store, or a native EmailStoreSession.</summary>
    [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true)]
    [Alias("Store")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Optional case-insensitive wildcard applied to folder names.</summary>
    [Parameter]
    public string? Name { get; set; }

    /// <summary>Optional well-known Outlook folder role.</summary>
    [Parameter]
    public EmailStoreSpecialFolderKind? SpecialFolderKind { get; set; }

    /// <summary>Returns only root folders.</summary>
    [Parameter]
    public SwitchParameter RootOnly { get; set; }

    /// <summary>Omits dynamic search folders from the results.</summary>
    [Parameter]
    public SwitchParameter ExcludeSearchFolders { get; set; }

    /// <summary>Writes matching lightweight folder records.</summary>
    protected override Task ProcessRecordAsync() {
        try {
            EmailStoreSession session = GetStoreSession(InputObject);
            WildcardPattern? pattern = string.IsNullOrWhiteSpace(Name)
                ? null
                : new WildcardPattern(Name, WildcardOptions.IgnoreCase);
            foreach (EmailStoreFolderInfo folder in session.Folders) {
                if (pattern != null && !pattern.IsMatch(folder.Name)) continue;
                if (SpecialFolderKind.HasValue && folder.SpecialFolderKind != SpecialFolderKind.Value) continue;
                if (RootOnly.IsPresent && folder.ParentId != null) continue;
                if (ExcludeSearchFolders.IsPresent && folder.IsSearchFolder) continue;
                WriteObject(folder);
            }
        } catch (Exception exception) {
            WriteError(new ErrorRecord(exception, "MailStoreFolderReadFailed", ErrorCategory.ReadError, InputObject));
        }
        return Task.CompletedTask;
    }
}
