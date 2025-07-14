using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Verifies PGP or S/MIME signatures on a <see cref="MimeMessage"/>.
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "MimeMessageSignature")]
[OutputType(typeof(bool))]
public sealed class CmdletTestMimeMessageSignature : PSCmdlet {
    /// <summary>Message to verify.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public MimeMessage? Message { get; set; }

    /// <summary>Public key for PGP signature verification.</summary>
    [Parameter(ParameterSetName = "Pgp", Mandatory = true)]
    public string? PublicKeyPath { get; set; }

    /// <summary>Certificates for S/MIME signature verification.</summary>
    [Parameter(ParameterSetName = "Smime", Mandatory = true)]
    public X509Certificate2[]? Certificate { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (Message == null) { WriteObject(false); return; }

        var enc = MimeKitUtils.GetEncryption(Message);
        bool result = enc switch {
            EmailEncryption.PgpSigned => MimeKitUtils.VerifyPgpSignature(Message, PublicKeyPath!),
            EmailEncryption.SmimeSigned => MimeKitUtils.VerifySmimeSignature(Message, Certificate!),
            _ => false
        };
        WriteObject(result);
    }
}
