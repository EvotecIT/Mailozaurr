namespace Mailozaurr.PowerShell;

using System.Management.Automation;
using System.Security;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Creates temporary cryptographic material for testing mail encryption.
/// </summary>
[Cmdlet(VerbsCommon.New, "TemporaryMailCrypto")]
[OutputType(typeof(TemporaryPgpKeyPair))]
[OutputType(typeof(X509Certificate2))]
public sealed class CmdletNewTemporaryMailCrypto : PSCmdlet {
    /// <summary>Generate a PGP key pair.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Pgp")]
    public SwitchParameter Pgp { get; set; }

    /// <summary>Identity for the PGP key.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    public string Identity { get; set; } = "Mailozaurr Test";

    /// <summary>Passphrase for the private key.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    public string PassPhrase { get; set; } = string.Empty;

    /// <summary>Size of the RSA key.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    public int KeySize { get; set; } = 2048;

    /// <summary>Optional path where the generated data should be stored.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    [Parameter(ParameterSetName = "Smime")]
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>Do not delete generated files when disposed.</summary>
    [Parameter(ParameterSetName = "Pgp")]
    [Parameter(ParameterSetName = "Smime")]
    public SwitchParameter NoDispose { get; set; }

    /// <summary>Generate an S/MIME certificate.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Smime")]
    public SwitchParameter Smime { get; set; }

    /// <summary>Subject for the certificate.</summary>
    [Parameter(ParameterSetName = "Smime")]
    public string SubjectName { get; set; } = "CN=Mailozaurr Test";

    /// <summary>Number of days the certificate is valid.</summary>
    [Parameter(ParameterSetName = "Smime")]
    public int ValidDays { get; set; } = 1;

    /// <summary>Password protecting an exported S/MIME PFX file.</summary>
    [Parameter(ParameterSetName = "Smime")]
    public SecureString? OutputPassword { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        if (ParameterSetName == "Pgp") {
            WriteObject(TemporaryPgpKeyPair.Create(Identity, PassPhrase, KeySize, OutputPath == string.Empty ? null : OutputPath, !NoDispose.IsPresent));
        } else {
            string? outputPassword = OutputPassword == null ? null : CredentialHelpers.ToPlainText(OutputPassword);
            WriteObject(TemporarySmimeCertificate.CreateSelfSigned(
                SubjectName,
                ValidDays,
                OutputPath == string.Empty ? null : OutputPath,
                outputPassword));
        }
    }
}
