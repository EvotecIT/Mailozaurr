using System.Management.Automation;
using System.Collections.Generic;
using MimeKit;
using System.IO;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsData.Save, "GraphMessageAttachment")]
[Alias("Save-MailMessageAttachment")]
public class CmdletSaveGraphMessageAttachment : PSCmdlet {
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public PSObject[]? Attachment { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    protected override void ProcessRecord() {
        var graphAttachments = new List<Attachment>();
        foreach (var att in Attachment) {
            if (att.BaseObject is Attachment ga) {
                graphAttachments.Add(ga);
            } else if (att.BaseObject is MimeKit.MimePart mp) {
                var resolved = System.IO.Path.GetFullPath(Path);
                if (!System.IO.Directory.Exists(resolved)) System.IO.Directory.CreateDirectory(resolved);
                var file = System.IO.Path.Combine(resolved, mp.FileName ?? System.IO.Path.GetRandomFileName());
                using var fs = System.IO.File.Create(file);
                mp.Content.DecodeTo(fs);
            }
        }
        if (graphAttachments.Count > 0) {
            MicrosoftGraphUtils.SaveAttachments(graphAttachments, Path);
        }
    }
}
