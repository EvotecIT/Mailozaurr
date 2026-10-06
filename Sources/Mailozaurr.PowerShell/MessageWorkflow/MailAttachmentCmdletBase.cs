using OfficeIMO.Email;

namespace Mailozaurr.PowerShell;

using EmailMessage = OfficeIMO.Email.EmailMessage;

/// <summary>PowerShell wildcard adaptation shared by attachment commands.</summary>
public abstract class MailAttachmentCmdletBase : AsyncPSCmdlet {
    /// <summary>Message or attachment description from Get-MailMessage or Get-MailAttachment.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public object? InputObject { get; set; }
    /// <summary>Filename wildcard patterns; a match against any pattern is sufficient.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? FileName { get; set; }
    /// <summary>Media-type wildcard patterns, such as image/*.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? ContentType { get; set; }
    /// <summary>Returns only attachments marked inline.</summary>
    [Parameter]
    public SwitchParameter InlineOnly { get; set; }
    /// <summary>Adapts a message or description and applies PowerShell filters.</summary>
    protected IEnumerable<EmailMessageAttachment> GetAttachments() {
        object? value = InputObject is PSObject ps ? ps.BaseObject : InputObject;
        IEnumerable<EmailMessageAttachment> attachments = value switch {
            EmailMessage message => message.Attachments,
            EmailMessageAttachment attachment => new[] { attachment },
            _ => throw new PSArgumentException("InputObject must be a Get-MailMessage message or Get-MailAttachment description.")
        };
        return attachments.Where(a => (!InlineOnly.IsPresent || a.IsInline) && Match(a.FileName, FileName) && Match(a.ContentType, ContentType));
    }
    private static bool Match(string? value, string[]? patterns) => patterns == null || patterns.Any(p =>
        new WildcardPattern(p, WildcardOptions.IgnoreCase | WildcardOptions.CultureInvariant).IsMatch(value ?? string.Empty));
}
