using System.Management.Automation;
using Mailozaurr;
using System.Linq;
using System.Collections.Generic;

namespace Mailozaurr.PowerShell {
    [Cmdlet(VerbsCommon.Get, "MailMessage")]
    [OutputType(typeof(PSObject))]
    public class GetMailMessageCmdlet : PSCmdlet {
        [Parameter(Mandatory = true)]
        public string UserPrincipalName { get; set; }

        [Parameter]
        public string ClientId { get; set; }
        [Parameter]
        public string ClientSecret { get; set; }
        [Parameter]
        public string DirectoryId { get; set; }
        [Parameter]
        public string[] Property { get; set; }
        [Parameter]
        public string Filter { get; set; }
        [Parameter]
        public int? Limit { get; set; }
        [Parameter]
        public PSCredential Credential { get; set; }

        protected override void ProcessRecord() {
            GraphCredential cred;
            if (Credential != null) {
                // Username is clientid@directoryid
                var parts = Credential.UserName.Split('@');
                if (parts.Length == 2) {
                    cred = new GraphCredential {
                        ClientId = parts[0],
                        DirectoryId = parts[1],
                        ClientSecret = Credential.GetNetworkCredential().Password
                    };
                } else {
                    ThrowTerminatingError(new ErrorRecord(new System.ArgumentException("Credential.UserName must be in the format clientid@directoryid"), "InvalidCredentialFormat", ErrorCategory.InvalidArgument, Credential));
                    return;
                }
            } else {
                cred = new GraphCredential { ClientId = ClientId, ClientSecret = ClientSecret, DirectoryId = DirectoryId };
            }
            var task = MicrosoftGraphUtils.GetMailMessagesAsync(cred, UserPrincipalName, Property, Filter, Limit);
            task.Wait();
            foreach (var dict in task.Result) {
                WriteObject(PSObject.AsPSObject(dict));
            }
        }
    }
}