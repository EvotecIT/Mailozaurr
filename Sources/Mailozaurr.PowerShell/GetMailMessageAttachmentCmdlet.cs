using System.Management.Automation;
using Mailozaurr;
using System.Linq;

namespace Mailozaurr.PowerShell {
    [Cmdlet(VerbsCommon.Get, "MailMessageAttachment")]
    [OutputType(typeof(Attachment))]
    public class GetMailMessageAttachmentCmdlet : PSCmdlet {
        [Parameter(Mandatory = true)]
        public string UserPrincipalName { get; set; }
        [Parameter(Mandatory = true)]
        public string MessageId { get; set; }
        [Parameter]
        public string ClientId { get; set; }
        [Parameter]
        public string ClientSecret { get; set; }
        [Parameter]
        public string DirectoryId { get; set; }
        [Parameter]
        public string[] Property { get; set; }

        protected override void ProcessRecord() {
            var cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
            var task = MicrosoftGraphUtils.GetMailMessageAttachmentsAsync(cred, UserPrincipalName, MessageId, Property);
            task.Wait();
            foreach (var att in task.Result) {
                WriteObject(att);
            }
        }
    }
}