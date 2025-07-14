using System.Management.Automation;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Saves a MIME message or wrapper object to disk.
/// </summary>
[Cmdlet(VerbsData.Save, "MimeMessage")]
public sealed class CmdletSaveMimeMessage : PSCmdlet {
    /// <summary>Message to save.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [Alias("Message")]
    public object? InputObject { get; set; }

    /// <summary>Destination file path.</summary>
    [Parameter(Mandatory = true)]
    public string? Path { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (InputObject == null || string.IsNullOrEmpty(Path)) return;
        MimeMessage? message = InputObject switch {
            MimeMessage m => m,
            ImapMessageInfo info => info.Raw.Message,
            ImapEmailMessage imap => imap.Message,
            Pop3MessageInfo pinfo => pinfo.Raw.Message,
            Pop3EmailMessage pop => pop.Message,
            GraphEmailMessage g => g.Message,
            _ => null
        };
        if (message == null) return;

        var resolved = System.IO.Path.GetFullPath(Path);
        var directory = System.IO.Path.GetDirectoryName(resolved);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);

        if (resolved.EndsWith(".msg", System.StringComparison.OrdinalIgnoreCase)) {
            var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid() + ".eml");
            try {
                message.WriteTo(temp);
                EmailMessage.ConvertEmlToMsg(new System.IO.FileInfo(temp), new System.IO.FileInfo(resolved), true);
            } finally {
                if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
            }
        } else {
            message.WriteTo(resolved);
        }
    }
}
