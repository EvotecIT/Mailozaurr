using System.Management.Automation;
using System.Security.Cryptography.X509Certificates;
using MimeKit;

namespace Mailozaurr.PowerShell;

/// <summary>
/// Decrypts an encrypted <see cref="MimeMessage"/> using PGP or S/MIME.
/// </summary>
[Cmdlet(VerbsSecurity.Unprotect, "MimeMessage")]
[OutputType(typeof(MimeMessage))]
public sealed class CmdletUnprotectMimeMessage : PSCmdlet {
    /// <summary>Object containing the MIME message.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, Position = 0)]
    [Alias("Message")]
    public object? InputObject { get; set; }

    /// <summary>Path to PGP private key.</summary>
    [Parameter]
    public string? PrivateKeyPath { get; set; }

    /// <summary>Password for PGP private key.</summary>
    [Parameter]
    public string? PrivateKeyPassword { get; set; }

    /// <summary>Certificate for S/MIME decryption.</summary>
    [Parameter]
    public X509Certificate2? Certificate { get; set; }

    /// <inheritdoc />
    protected override void ProcessRecord() {
        MimeMessage? message = InputObject switch {
            MimeMessage m => m,
            ImapMessageInfo info => info.Raw.Message,
            ImapEmailMessage imap => imap.Message,
            Pop3MessageInfo pinfo => pinfo.Raw.Message,
            Pop3EmailMessage pop => pop.Message,
            GraphEmailMessage g => g.Message,
            _ => null
        };

        if (message == null) {
            WriteObject(InputObject);
            return;
        }

        var enc = MimeKitUtils.GetEncryption(message);
        switch (enc) {
            case EmailEncryption.PgpEncrypted:
                if (string.IsNullOrEmpty(PrivateKeyPath)) {
                    ThrowTerminatingError(new ErrorRecord(new PSArgumentException("PrivateKeyPath is required"), "MissingPrivateKeyPath", ErrorCategory.InvalidArgument, InputObject));
                }
                var decryptedPgp = MimeKitUtils.DecryptPgp(message, PrivateKeyPath!, PrivateKeyPassword ?? string.Empty);
                WriteObject(decryptedPgp);
                break;
            case EmailEncryption.SmimeEncrypted:
                if (Certificate == null) {
                    ThrowTerminatingError(new ErrorRecord(new PSArgumentException("Certificate is required"), "MissingCertificate", ErrorCategory.InvalidArgument, InputObject));
                }
                var decryptedSmime = MimeKitUtils.DecryptSmime(message, Certificate!);
                WriteObject(decryptedSmime);
                break;
            default:
                WriteObject(message);
                break;
        }
    }
}
