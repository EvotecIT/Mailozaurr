using Mailozaurr.Definitions;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Creates an attachment descriptor for Mailozaurr send cmdlets.
/// </summary>
[Cmdlet(VerbsCommon.New, "EmailAttachment", DefaultParameterSetName = PathParameterSet)]
[OutputType(typeof(AttachmentDescriptor))]
public sealed class CmdletNewEmailAttachment : PSCmdlet {
    private const string PathParameterSet = "Path";
    private const string BytesParameterSet = "Bytes";
    private const string TextParameterSet = "Text";
    private const string StreamParameterSet = "Stream";

    /// <summary>File path used as attachment content.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PathParameterSet)]
    public string? Path { get; set; }

    /// <summary>Attachment content as bytes.</summary>
    [Parameter(Mandatory = true, ParameterSetName = BytesParameterSet)]
    public byte[]? Bytes { get; set; }

    /// <summary>Attachment content as text.</summary>
    [Parameter(Mandatory = true, ParameterSetName = TextParameterSet)]
    public string? Text { get; set; }

    /// <summary>Attachment content as a readable stream.</summary>
    [Parameter(Mandatory = true, ParameterSetName = StreamParameterSet)]
    public Stream? Stream { get; set; }

    /// <summary>File name to use for non-path attachment content.</summary>
    [Parameter(ParameterSetName = BytesParameterSet)]
    [Parameter(ParameterSetName = TextParameterSet)]
    [Parameter(ParameterSetName = StreamParameterSet)]
    public string? FileName { get; set; }

    /// <summary>MIME content type for the attachment.</summary>
    [Parameter]
    public string? ContentType { get; set; }

    /// <summary>Content id used when the attachment is sent inline.</summary>
    [Parameter]
    public string? ContentId { get; set; }

    /// <summary>Text encoding used with Text content. Defaults to UTF-8.</summary>
    [Parameter(ParameterSetName = TextParameterSet)]
    public Encoding? Encoding { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        AttachmentDescriptor descriptor = ParameterSetName switch {
            PathParameterSet => new FileAttachmentDescriptor(Path!),
            BytesParameterSet => new ByteArrayAttachmentDescriptor(Bytes!, RequireFileName()),
            TextParameterSet => new ByteArrayAttachmentDescriptor((Encoding ?? Encoding.UTF8).GetBytes(Text!), RequireFileName()),
            StreamParameterSet => new StreamAttachmentDescriptor(Stream!, RequireFileName()),
            _ => throw new InvalidOperationException($"Unsupported parameter set '{ParameterSetName}'.")
        };

        if (!string.IsNullOrWhiteSpace(ContentType)) {
            descriptor.ContentType = ContentType;
        }

        if (!string.IsNullOrWhiteSpace(ContentId)) {
            descriptor.ContentId = ContentId;
        }

        WriteObject(descriptor);
    }

    private string RequireFileName() {
        if (string.IsNullOrWhiteSpace(FileName)) {
            ThrowTerminatingError(new ErrorRecord(
                new PSArgumentException("FileName is required for in-memory attachment content."),
                "MissingFileName",
                ErrorCategory.InvalidArgument,
                null));
        }

        return FileName!;
    }
}
