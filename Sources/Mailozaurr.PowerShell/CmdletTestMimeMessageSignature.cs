using MimeKit;
using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Verifies PGP or S/MIME signatures on a <see cref="MimeMessage"/>.
/// </summary>
[Cmdlet(VerbsDiagnostic.Test, "MimeMessageSignature", DefaultParameterSetName = "Auto")]
[OutputType(typeof(bool))]
public sealed class CmdletTestMimeMessageSignature : PSCmdlet {
    /// <summary>Message to verify.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [Alias("Message")]
    [ValidateNotNull]
    public object? InputObject { get; set; }

    /// <summary>Public key for PGP signature verification.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    public string? PublicKeyPath { get; set; }

    /// <summary>Certificates for S/MIME signature verification.</summary>
    [Parameter(ParameterSetName = "Smime")]
    public X509Certificate2[]? Certificate { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        var message = PowerShellMimeMessageResolver.Resolve(InputObject);
        if (message == null) { WriteObject(false); return; }

        var enc = MimeKitUtils.GetEncryption(message);
        bool result = enc switch {
            EmailEncryption.PgpSigned => MimeKitUtils.VerifyPgpSignature(message, PublicKeyPath!),
            EmailEncryption.SmimeSigned => MimeKitUtils.VerifySmimeSignature(message, Certificate!),
            _ => false
        };
        WriteObject(result);
    }
}