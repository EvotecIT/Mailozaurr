using System.Management.Automation;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Mailozaurr.PowerShell;

[Cmdlet(VerbsCommunications.Connect, "OAuthO365")]
[OutputType(typeof(PSCredential))]
public class CmdletConnectOAuthO365 : PSCmdlet {
    [Parameter(Mandatory = false)]
    public string Login { get; set; }

    [Parameter(Mandatory = true)]
    public string ClientID { get; set; }

    [Parameter(Mandatory = true)]
    public string TenantID { get; set; }

    [Parameter(Mandatory = false)]
    public string RedirectUri { get; set; } = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    [Parameter(Mandatory = false)]
    public string[] Scopes { get; set; } = new[] {
        "email",
        "offline_access",
        "https://outlook.office.com/IMAP.AccessAsUser.All",
        "https://outlook.office.com/POP.AccessAsUser.All",
        "https://outlook.office.com/SMTP.Send"
    };

    protected override void ProcessRecord() {
        Mailozaurr.Authentication.OAuthCredential cred = null;
        try {
            cred = Task.Run(() => Mailozaurr.Authentication.OAuthHelpers.AcquireO365TokenInteractiveAsync(Login, ClientID, TenantID, RedirectUri, Scopes)).GetAwaiter().GetResult();
        } catch (System.Exception ex) {
            WriteError(new ErrorRecord(ex, "OAuthO365AuthFailed", ErrorCategory.AuthenticationError, null));
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