using System.Management.Automation;
using System.Threading.Tasks;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommunications.Connect, "OAuthGoogle")]
[OutputType(typeof(PSCredential))]
public class CmdletConnectOAuthGoogle : PSCmdlet {
    [Parameter(Mandatory = true)]
    public string GmailAccount { get; set; }

    [Parameter(Mandatory = true)]
    public string ClientID { get; set; }

    [Parameter(Mandatory = true)]
    public string ClientSecret { get; set; }

    [Parameter(Mandatory = false)]
    public string[] Scope { get; set; } = new[] { "https://mail.google.com/" };

    protected override void ProcessRecord() {
        Mailozaurr.Authentication.OAuthCredential cred = null;
        try {
            cred = Task.Run(() => Mailozaurr.Authentication.OAuthHelpers.AcquireGoogleTokenInteractiveAsync(GmailAccount, ClientID, ClientSecret, Scope)).GetAwaiter().GetResult();
        } catch (System.Exception ex) {
            WriteError(new ErrorRecord(ex, "OAuthGoogleAuthFailed", ErrorCategory.AuthenticationError, null));
            return;
        }
        if (cred != null) {
            var secure = new System.Security.SecureString();
            foreach (var c in cred.AccessToken) secure.AppendChar(c);
            var psCred = new PSCredential(cred.UserName, secure);
            WriteObject(psCred);
        }
    }
}