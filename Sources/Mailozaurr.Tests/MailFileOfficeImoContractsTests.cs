using MimeKit;
using MimeKit.Cryptography;
using OfficeIMO.Email;
using System.Reflection;

namespace Mailozaurr.Tests;

public sealed class MailFileOfficeImoContractsTests {
    [Fact]
    public void MailFileApisLiveInMainAssemblyWithoutLegacyMsgDependencyForest() {
        Assembly assembly = typeof(MailFileReader).Assembly;
        Assert.Same(assembly, typeof(MimeKitUtils).Assembly);

        string[] references = assembly.GetReferencedAssemblies().Select(item => item.Name!).ToArray();
        Assert.Contains("OfficeIMO.Email", references);
        Assert.Contains("MimeKit", references);
        Assert.DoesNotContain("Mailozaurr.Msg", references);
        Assert.DoesNotContain("MsgKit", references);
        Assert.DoesNotContain("MsgReader", references);
        Assert.DoesNotContain("OpenMcdf", references);
        Assert.DoesNotContain("RtfPipe", references);
        Assert.DoesNotContain("OfficeIMO.Shared", references);
    }

    [Fact]
    public void CompatibilityRecipientValuesRemainStable() {
        Assert.Equal(0, (int)MailFileRecipientType.Unknown);
        Assert.Equal(1, (int)MailFileRecipientType.To);
        Assert.Equal(2, (int)MailFileRecipientType.Cc);
        Assert.Equal(3, (int)MailFileRecipientType.Bcc);
        Assert.Equal(4, (int)MailFileRecipientType.Resource);
        Assert.Equal(5, (int)MailFileRecipientType.Room);
        Assert.Equal(6, (int)MailFileRecipientType.ReplyTo);
    }

    [Fact]
    public void EmlImportExposesMimeKitAndOfficeImoOwnerModels() {
        string directory = CreateTempDirectory();
        try {
            string path = WriteEml(directory, "owner.eml", "Owner models", "Hello owner");

            MailFileMessage result = MailFileReader.Read(path,
                new MailFileReaderOptions { IncludeHeaders = true });

            Assert.Equal(EmailFileFormat.Eml, result.OfficeDocument.Format);
            Assert.Equal("Owner models", result.OfficeDocument.Subject);
            Assert.NotNull(result.MimeMessage);
            Assert.Equal("Owner models", result.MimeMessage!.Subject);
            Assert.Equal("Hello owner", result.BodyText!.Trim());
            Assert.NotNull(result.Headers);
            Assert.DoesNotContain(result.Diagnostics,
                item => item.Severity == EmailDiagnosticSeverity.Error);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MsgImportExposesTypedOutlookContactAndMimeProjection() {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, "contact.msg");
            var document = new EmailDocument {
                Format = EmailFileFormat.OutlookMsg,
                OutlookItemKind = OutlookItemKind.Contact,
                Subject = "Ada Lovelace",
                Contact = new OutlookContact {
                    DisplayName = "Ada Lovelace",
                    GivenName = "Ada",
                    Surname = "Lovelace",
                    CompanyName = "Analytical"
                }
            };
            document.Contact.Email1.Address = "ada@example.com";
            new EmailDocumentWriter().Write(document, path, EmailFileFormat.OutlookMsg);

            MailFileMessage result = MailFileReader.Read(path);

            Assert.Equal(OutlookItemKind.Contact, result.OutlookItemKind);
            Assert.Equal("IPM.Contact", result.MessageClass);
            Assert.Equal("Ada", result.OfficeDocument.Contact!.GivenName);
            Assert.Equal("ada@example.com", result.OfficeDocument.Contact.Email1.Address);
            Assert.Null(result.MimeMessage);
            Assert.Equal("Ada Lovelace", result.ToMimeMessage().Subject);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task AsyncReadAndConversionUseTheSameOwnerContracts() {
        string directory = CreateTempDirectory();
        try {
            string emlPath = WriteEml(directory, "async.eml", "Async owner", "Async body");
            string msgPath = Path.Combine(directory, "async.msg");
            string roundTripPath = Path.Combine(directory, "async-roundtrip.eml");

            EmlConversionResult toMsg = await EmailMessage.ConvertEmlToMsgAsync(
                new FileInfo(emlPath), new FileInfo(msgPath), true);
            MailFileMessage imported = await MailFileReader.ReadAsync(msgPath);
            MsgConversionResult toEml = await EmailMessage.ConvertMsgToEmlAsync(
                new FileInfo(msgPath), new FileInfo(roundTripPath), true);

            Assert.True(toMsg.Status, toMsg.Error);
            Assert.Equal("Async owner", imported.Subject);
            Assert.Equal(EmailFileFormat.OutlookMsg, imported.OfficeDocument.Format);
            Assert.True(toEml.Status, toEml.Error);
            Assert.Equal("Async owner", MimeMessage.Load(roundTripPath).Subject);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task ConversionObservesFilesCreatedAfterFileInfoWasInspected() {
        string directory = CreateTempDirectory();
        try {
            string emlPath = Path.Combine(directory, "stale.eml");
            string msgPath = Path.Combine(directory, "stale.msg");
            string roundTripPath = Path.Combine(directory, "stale-roundtrip.eml");
            var emlFile = new FileInfo(emlPath);
            var msgFile = new FileInfo(msgPath);
            var roundTripFile = new FileInfo(roundTripPath);
            Assert.False(emlFile.Exists);
            Assert.False(msgFile.Exists);
            Assert.False(roundTripFile.Exists);

            WriteEml(directory, "stale.eml", "Fresh metadata", "Fresh body");

            EmlConversionResult toMsg = await EmailMessage.ConvertEmlToMsgAsync(emlFile, msgFile, true);
            MsgConversionResult toEml = await EmailMessage.ConvertMsgToEmlAsync(msgFile, roundTripFile, true);

            Assert.True(toMsg.Status, toMsg.Error);
            Assert.True(toEml.Status, toEml.Error);
            Assert.Equal("Fresh metadata", MimeMessage.Load(roundTripPath).Subject);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SignedEmlProjectsVerifiedSignatureCompatibilityFields() {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, "signed.eml");
            using var certificate = TemporarySmimeCertificate.CreateSelfSigned("CN=Mail File Signer");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Signer", "signer@example.com"));
            message.To.Add(new MailboxAddress("Recipient", "recipient@example.com"));
            message.Subject = "Signed owner";
            message.Body = new TextPart("plain") { Text = "Signed body" };
            using (var context = new TemporarySecureMimeContext()) {
                var signer = new CmsSigner(certificate) { DigestAlgorithm = DigestAlgorithm.Sha256 };
                message.Body = MultipartSigned.Create(context, signer, message.Body);
            }
            message.WriteTo(path);

            MailFileMessage result = MailFileReader.Read(path);

            Assert.True(result.SignatureIsValid);
            Assert.Contains("Mail File Signer", result.SignedBy, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.SignedOn);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ProtectedMsgPayloadCanBeHandedToMimeKit() {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, "protected.msg");
            byte[] cms = { 0x30, 0x03, 0x02, 0x01, 0x01 };
            var document = new EmailDocument {
                Format = EmailFileFormat.OutlookMsg,
                MessageClass = "IPM.Note.SMIME",
                Subject = "Protected"
            };
            document.Attachments.Add(new EmailAttachment {
                FileName = "smime.p7m",
                ContentType = "application/pkcs7-mime; smime-type=enveloped-data",
                Content = cms,
                Length = cms.Length
            });
            new EmailDocumentWriter().Write(document, path, EmailFileFormat.OutlookMsg);

            MailFileMessage result = MailFileReader.Read(path);

            Assert.Equal(EmailProtectionKind.SmimeOpaque, result.ProtectionKind);
            Assert.True(result.TryGetProtectedMimeEntity(out MimeEntity? entity));
            Assert.NotNull(entity);
            Assert.Equal("application/pkcs7-mime", entity!.ContentType.MimeType);
            entity.Dispose();
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MalformedProtectedContentTypeReturnsFalse() {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, "malformed-protected.msg");
            var document = new EmailDocument {
                Format = EmailFileFormat.OutlookMsg,
                MessageClass = "IPM.Note.SMIME",
                Subject = "Malformed protected metadata"
            };
            document.Attachments.Add(new EmailAttachment {
                FileName = "smime.p7m",
                ContentType = "not-a-content-type",
                Content = new byte[] { 1, 2, 3 },
                Length = 3
            });
            new EmailDocumentWriter().Write(document, path, EmailFileFormat.OutlookMsg);

            MailFileMessage result = MailFileReader.Read(path);

            Assert.Equal(EmailProtectionKind.SmimeOpaque, result.ProtectionKind);
            Assert.False(result.TryGetProtectedMimeEntity(out MimeEntity? entity));
            Assert.Null(entity);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AttachmentContentCanBeBoundedWithoutLosingMetadata() {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, "bounded.msg");
            var document = new EmailDocument { Subject = "Bounded" };
            document.Attachments.Add(new EmailAttachment {
                FileName = "payload.bin",
                ContentType = "application/octet-stream",
                Content = new byte[] { 1, 2, 3, 4 },
                Length = 4
            });
            new EmailDocumentWriter().Write(document, path, EmailFileFormat.OutlookMsg);

            MailFileMessage result = MailFileReader.Read(path, new MailFileReaderOptions {
                IncludeAttachmentContent = false
            });

            MailFileAttachment attachment = Assert.Single(result.Attachments);
            Assert.Equal("payload.bin", attachment.FileName);
            Assert.Equal(4, attachment.Size);
            Assert.Null(attachment.Content);
            Assert.Null(Assert.Single(result.OfficeDocument.Attachments).Content);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    private static string CreateTempDirectory() {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteEml(string directory, string fileName, string subject, string body) {
        string path = Path.Combine(directory, fileName);
        File.WriteAllText(path, string.Join("\r\n", new[] {
            "From: Alice <alice@example.com>",
            "To: Bob <bob@example.com>",
            $"Subject: {subject}",
            "Date: Mon, 21 Jun 2021 10:00:00 +0000",
            "Message-ID: <owner@example.com>",
            "MIME-Version: 1.0",
            "Content-Type: text/plain; charset=utf-8",
            string.Empty,
            body
        }));
        return path;
    }
}
