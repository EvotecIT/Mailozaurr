namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for Mailgun API authentication from an API key.</para>
/// <para type="description">Use <c>ConvertTo-MailgunCredential</c> to generate a <see cref="PSCredential"/> that can be passed to <c>Send-EmailMessage</c> when using the Mailgun provider.</para>
/// <example>
///   <summary>Create a Mailgun credential</summary>
///   <code>ConvertTo-MailgunCredential -ApiKey "key-xxxxx"</code>
/// </example>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "MailgunCredential")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToMailgunCredential : PSCmdlet {
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string? ApiKey { get; set; }

    protected override void ProcessRecord() {
        SecureString secret = CredentialHelpers.ToSecureString(ApiKey);
        var credential = new PSCredential("Mailgun", secret);
        WriteObject(credential);
    }
}
