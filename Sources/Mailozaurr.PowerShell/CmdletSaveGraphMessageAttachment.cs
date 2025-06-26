using System.Management.Automation;
using System.Collections.Generic;
using MimeKit;
using System.IO;
using Mailozaurr;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsData.Save, "GraphMessageAttachment")]
public class CmdletSaveGraphMessageAttachment : PSCmdlet {
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNullOrEmpty]
    public PSObject[]? Attachment { get; set; }

    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    protected override void ProcessRecord() {
        var graphAttachments = new List<Attachment>();
        var mimeAttachments = new List<MimeEntity>();
        foreach (var att in Attachment) {
            if (att.BaseObject is Attachment ga) {
                graphAttachments.Add(ga);
            } else if (att.BaseObject is MimeEntity me) {
                mimeAttachments.Add(me);
            }
        }
        if (mimeAttachments.Count > 0) {
            MimeKitUtils.SaveAttachments(mimeAttachments, Path);
        }
        if (graphAttachments.Count > 0) {
            MicrosoftGraphUtils.SaveAttachments(graphAttachments, Path);
        }
    }
}
