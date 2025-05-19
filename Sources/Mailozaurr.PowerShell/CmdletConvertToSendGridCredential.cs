namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;

/// <summary>
/// <para type="synopsis">Creates a PSCredential object for SendGrid API authentication from an API key.</para>
/// <para type="description">The <c>ConvertTo-SendGridCredential</c> cmdlet creates a <see cref="PSCredential"/> object suitable for SendGrid API authentication, using the provided API key. The resulting credential can be used with cmdlets that require SendGrid authentication (such as <c>Send-EmailMessage</c> with the <c>-SendGrid</c> switch).</para>
/// <example>
///   <summary>Create a SendGrid credential</summary>
///   <code>ConvertTo-SendGridCredential -ApiKey "SG.xxxxx..."</code>
/// </example>
/// <remarks>
/// Use this cmdlet to prepare credentials for use with SendGrid-enabled cmdlets in automation scenarios.
/// </remarks>
/// <seealso href="https://github.com/EvotecIT/Mailozaurr">Mailozaurr Documentation</seealso>
/// </summary>
[Cmdlet(VerbsData.ConvertTo, "SendGridCredential")]
[OutputType(typeof(PSCredential))]
public class CmdletConvertToSendGridCredential : PSCmdlet {
    /// <summary>
    /// <para type="description">Specifies the SendGrid API key.</para>
    /// </summary>
    [Parameter(Mandatory = true)]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Creates a PSCredential object for SendGrid API authentication.
    /// </summary>
    protected override void ProcessRecord() {
        var secret = CredentialHelpers.ToSecureString(ApiKey);
        var credential = new PSCredential("SendGrid", secret);
        WriteObject(credential);
    }
}