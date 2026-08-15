using MimeKit;
using MimeKit.Cryptography;
using OfficeIMO.Email;
using System.Reflection;

namespace Mailozaurr.Tests;

public sealed class MailFileOfficeImoContractsTests {
    [Fact]
    public void MailFileApisAvoidConcreteSecurityAndLegacyMsgDependencies() {
        Assembly assembly = typeof(MailFileReader).Assembly;
        Assert.Same(assembly, typeof(MimeKitUtils).Assembly);

        string[] references = assembly.GetReferencedAssemblies().Select(item => item.Name!).ToArray();
        Assert.Contains("OfficeIMO.Email", references);
        Assert.Contains("MimeKit", references);
        Assert.DoesNotContain("OfficeIMO.Security", references);
        Assert.DoesNotContain("Mailozaurr.Msg", references);
        Assert.DoesNotContain("MsgKit", references);
        Assert.DoesNotContain("MsgReader", references);
        Assert.DoesNotContain("OpenMcdf", references);
        Assert.DoesNotContain("RtfPipe", references);
        Assert.DoesNotContain("OfficeIMO.Shared", references);

        string[] powerShellReferences = typeof(Mailozaurr.PowerShell.CmdletImportMailFile).Assembly
            .GetReferencedAssemblies().Select(item => item.Name!).ToArray();
        Assert.Contains("OfficeIMO.Security", powerShellReferences);
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
    public void EmlImportExposesOfficeImoOwnerAndCurrentMimeProjection() {
        string directory = CreateTempDirectory();
        try {
            string path = WriteEml(directory, "owner.eml", "Owner models", "Hello owner");

            MailFileMessage result = MailFileReader.Read(path,
                new MailFileReaderOptions { IncludeHeaders = true });

            Assert.Equal(EmailFileFormat.Eml, result.OfficeDocument.Format);
            Assert.Equal("Owner models", result.OfficeDocument.Subject);
            using MimeMessage projected = result.ToMimeMessage();
            Assert.Equal("Owner models", projected.Subject);
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
            using MimeMessage projected = result.ToMimeMessage();
            Assert.Equal("Ada Lovelace", projected.Subject);
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
    public async Task MailFileMessageIsTheSimpleLoadAndSaveEntryPoint() {
        string directory = CreateTempDirectory();
        try {
            string emlPath = WriteEml(directory, "simple.eml", "Simple Mailozaurr API", "Simple body");
            string msgPath = Path.Combine(directory, "simple.msg");
            string roundTripPath = Path.Combine(directory, "simple-roundtrip.eml");

            MailFileMessage message = MailFileMessage.Load(emlPath);
            await message.SaveAsync(msgPath);
            MailFileMessage converted = await MailFileMessage.LoadAsync(msgPath);
            converted.Save(roundTripPath);

            Assert.Equal("Simple Mailozaurr API", message.Subject);
            Assert.False(message.HasErrors);
            Assert.Equal(MailFileFormat.Msg, converted.Format);
            Assert.False(converted.HasErrors);
            Assert.Equal("Simple Mailozaurr API", MimeMessage.Load(roundTripPath).Subject);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MailFileMessageSavePreservesExistingOutputWhenWriterDiagnosticsContainErrors() {
        string directory = CreateTempDirectory();
        try {
            string sourcePath = WriteEml(directory, "invalid-save-source.eml", "Invalid save", "Body");
            string outputPath = Path.Combine(directory, "existing.eml");
            const string existingContent = "existing artifact";
            File.WriteAllText(outputPath, existingContent);
            MailFileMessage message = MailFileMessage.Load(sourcePath);
            message.OfficeDocument.Attachments.Add(new EmailAttachment {
                FileName = "missing.bin",
                ContentType = "application/octet-stream",
                Length = 10
            });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => message.Save(outputPath));

            Assert.Contains("EMAIL_ATTACHMENT_CONTENT_UNAVAILABLE", exception.Message, StringComparison.Ordinal);
            Assert.Equal(existingContent, File.ReadAllText(outputPath));
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CompatibilityProjectionConversionAndSaveUseTheCurrentOfficeDocument() {
        string directory = CreateTempDirectory();
        try {
            string sourcePath = WriteEml(directory, "mutable.eml", "Original subject", "Original body");
            string savedPath = Path.Combine(directory, "mutable-saved.eml");
            MailFileMessage message = MailFileMessage.Load(sourcePath,
                new MailFileReaderOptions { IncludeHeaders = true });

            message.OfficeDocument.Subject = "Updated subject";
            message.OfficeDocument.Body.Text = "Updated body";

            Assert.Equal("Updated subject", message.Subject);
            Assert.Equal("Updated body", message.BodyText);
            using (MimeMessage projected = message.ToMimeMessage()) {
                Assert.Equal("Updated subject", projected.Subject);
                Assert.Equal("Updated body", projected.TextBody);
            }

            message.Save(savedPath);
            using MimeMessage saved = MimeMessage.Load(savedPath);
            Assert.Equal("Updated subject", saved.Subject);
            Assert.Equal("Updated body", saved.TextBody);
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
            var signedBody = new Multipart("mixed") {
                new TextPart("plain") { Text = "Signed body" },
                new MimePart("application", "octet-stream") {
                    Content = new MimeContent(new MemoryStream(new byte[] { 1, 2, 3, 4 })),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    FileName = "signed.bin"
                }
            };
            using (var context = new TemporarySecureMimeContext()) {
                var signer = new CmsSigner(certificate) { DigestAlgorithm = DigestAlgorithm.Sha256 };
                message.Body = MultipartSigned.Create(context, signer, signedBody);
            }
            message.WriteTo(path);

            string? expectedSignerName;
            DateTimeOffset expectedSigningTime;
            using (var context = new TemporarySecureMimeContext()) {
                DigitalSignatureCollection signatures = ((MultipartSigned)message.Body).Verify(context);
                IDigitalSignature signature = Assert.Single(signatures);
                expectedSignerName = !string.IsNullOrWhiteSpace(signature.SignerCertificate?.Name)
                    ? signature.SignerCertificate!.Name
                    : signature.SignerCertificate?.Email;
                expectedSigningTime = signature.CreationDate;
            }

            using MailFileMessage unverified = MailFileReader.Read(path);
            Assert.Null(unverified.SignatureIsValid);
            Assert.Null(unverified.SignedBy);
            Assert.Null(unverified.SignedOn);

            using MailFileMessage result = MailFileReader.Read(path,
                new MailFileReaderOptions {
                    VerifySignature = true,
                    SecurityProvider = OfficeIMO.Security.OfficeSecurityProvider.Default,
                    OfficeReaderOptions = new EmailReaderOptions(includeAttachmentContent: false)
                });

            Assert.True(result.SignatureIsValid);
            Assert.NotNull(result.SignatureVerification);
            Assert.True(result.SignatureVerification!.IsCryptographicallyValid);
            Assert.Equal(expectedSignerName, result.SignedBy);
            Assert.Equal(expectedSigningTime, result.SignedOn);
            EmailAttachment signedAttachment = Assert.Single(result.SignatureVerification.SignedContent!.Attachments);
            Assert.Equal("signed.bin", signedAttachment.FileName);
            Assert.Equal(4, signedAttachment.Length);
            Assert.Null(signedAttachment.Content);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SignatureVerificationRequiresAnExplicitSecurityProvider() {
        string directory = CreateTempDirectory();
        try {
            string path = WriteEml(directory, "provider-required.eml", "Provider required", "Body");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                MailFileReader.Read(path, new MailFileReaderOptions { VerifySignature = true }));

            Assert.Contains("IOfficeSecurityProvider", exception.Message, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("message.oft", EmailFileFormat.OutlookTemplate, MailFileFormat.OutlookTemplate)]
    [InlineData("winmail.dat", EmailFileFormat.Tnef, MailFileFormat.Tnef)]
    public void ImportSupportsOutlookTemplateAndTnefArtifacts(
        string fileName,
        EmailFileFormat officeFormat,
        MailFileFormat expectedFormat) {
        string directory = CreateTempDirectory();
        try {
            string path = Path.Combine(directory, fileName);
            var document = new EmailDocument {
                Subject = "Expanded mail-file input",
                Body = { Text = "Owner-backed content" }
            };
            new EmailDocumentWriter().Write(document, path, officeFormat);

            using MailFileMessage result = MailFileReader.Read(path);

            Assert.Equal(expectedFormat, result.Format);
            Assert.Equal(officeFormat, result.OfficeDocument.Format);
            Assert.Equal("Expanded mail-file input", result.Subject);
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
    public async Task MimeProjectionRejectsOfficeImoWriterDiagnostics() {
        var document = new EmailDocument { Subject = "Incomplete attachment" };
        document.Attachments.Add(new EmailAttachment {
            FileName = "missing.bin",
            ContentType = "application/octet-stream",
            Length = 4,
            Content = null
        });

        MailFileMimeMessageConversionResult syncConversion =
            MailFileMimeAdapter.ConvertToMimeMessage(document);
        MailFileMimeMessageConversionResult asyncConversion =
            await MailFileMimeAdapter.ConvertToMimeMessageAsync(document);
        InvalidDataException syncError = Assert.Throws<InvalidDataException>(
            () => MailFileMimeAdapter.ToMimeMessage(document));
        InvalidDataException asyncError = await Assert.ThrowsAsync<InvalidDataException>(
            () => MailFileMimeAdapter.ToMimeMessageAsync(document));

        Assert.True(syncConversion.HasErrors);
        Assert.Null(syncConversion.Message);
        Assert.Contains(syncConversion.Diagnostics,
            item => item.Code == "EMAIL_ATTACHMENT_CONTENT_UNAVAILABLE");
        Assert.True(asyncConversion.HasErrors);
        Assert.Null(asyncConversion.Message);
        Assert.Contains(asyncConversion.Diagnostics,
            item => item.Code == "EMAIL_ATTACHMENT_CONTENT_UNAVAILABLE");
        Assert.Contains("EMAIL_ATTACHMENT_CONTENT_UNAVAILABLE", syncError.Message, StringComparison.Ordinal);
        Assert.Contains("EMAIL_ATTACHMENT_CONTENT_UNAVAILABLE", asyncError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MimeMessageConvertsToFileBackedOfficeDocumentWithOwnedLifetime() {
        byte[] content = Enumerable.Range(0, 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Alice", "alice@example.com"));
        message.To.Add(new MailboxAddress("Bob", "bob@example.com"));
        message.Subject = "Mime to OfficeIMO";
        message.Headers.Add("X-Trace-ID", "mime-officeimo-bridge");
        message.Body = new Multipart("mixed") {
            new TextPart("plain") { Text = "Bridge body" },
            new MimePart("application", "octet-stream") {
                Content = new MimeContent(new MemoryStream(content, writable: false)),
                ContentTransferEncoding = ContentEncoding.Base64,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                FileName = "payload.bin"
            }
        };

        MailFileEmailDocumentConversionResult conversion =
            MailFileMimeAdapter.ConvertToEmailDocument(message);
        EmailAttachment attachment = Assert.Single(conversion.Document.Attachments);

        Assert.False(conversion.HasErrors);
        Assert.Equal(EmailFileFormat.Eml, conversion.Document.Format);
        Assert.Equal("Mime to OfficeIMO", conversion.Document.Subject);
        Assert.Equal("Bridge body", conversion.Document.Body.Text);
        Assert.Contains(conversion.Document.Headers,
            item => item.Name == "X-Trace-ID" && item.Value == "mime-officeimo-bridge");
        Assert.True(conversion.UsesFileBackedContent);
        Assert.Null(attachment.Content);
        Assert.NotNull(attachment.ContentSource);
        using (Stream stream = attachment.OpenContentStream()) {
            Assert.Equal(content.Length, stream.Length);
            Assert.Equal(content[1024], ReadByteAt(stream, 1024));
        }

        conversion.Dispose();
        Assert.Throws<ObjectDisposedException>(() => attachment.OpenContentStream());
    }

    [Fact]
    public async Task MimeMessageConvertsAsynchronouslyToMaterializedOfficeDocument() {
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("alice@example.com"));
        message.To.Add(MailboxAddress.Parse("bob@example.com"));
        message.Subject = "Async Mime to OfficeIMO";
        message.Body = new Multipart("mixed") {
            new TextPart("plain") { Text = "Async bridge body" },
            new MimePart("application", "octet-stream") {
                Content = new MimeContent(new MemoryStream(new byte[] { 9, 8, 7, 6 }, writable: false)),
                ContentTransferEncoding = ContentEncoding.Base64,
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                FileName = "async.bin"
            }
        };

        using MailFileEmailDocumentConversionResult conversion =
            await MailFileMimeAdapter.ConvertToEmailDocumentAsync(message, useFileBackedContent: false);
        EmailAttachment attachment = Assert.Single(conversion.Document.Attachments);

        Assert.False(conversion.HasErrors);
        Assert.False(conversion.UsesFileBackedContent);
        Assert.Equal("Async bridge body", conversion.Document.Body.Text);
        Assert.Equal(new byte[] { 9, 8, 7, 6 }, attachment.Content);
        Assert.Null(attachment.ContentSource);
    }

    [Fact]
    public async Task MimeMessageConversionEnforcesOfficeImoInputLimitBeforeParsing() {
        using var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("alice@example.com"));
        message.To.Add(MailboxAddress.Parse("bob@example.com"));
        message.Subject = "Bounded conversion";
        message.Body = new TextPart("plain") { Text = new string('x', 8192) };
        var options = new EmailReaderOptions(maxInputBytes: 1024);

        EmailLimitExceededException syncError = Assert.Throws<EmailLimitExceededException>(() =>
            MailFileMimeAdapter.ConvertToEmailDocument(message, options));
        EmailLimitExceededException asyncError = await Assert.ThrowsAsync<EmailLimitExceededException>(() =>
            MailFileMimeAdapter.ConvertToEmailDocumentAsync(message, options));

        Assert.Equal(nameof(EmailReaderOptions.MaxInputBytes), syncError.LimitName);
        Assert.Equal(nameof(EmailReaderOptions.MaxInputBytes), asyncError.LimitName);
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

    private static int ReadByteAt(Stream stream, long offset) {
        stream.Position = offset;
        return stream.ReadByte();
    }
}
