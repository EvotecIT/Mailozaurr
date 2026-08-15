# Mailozaurr 3.0 package migration

Mailozaurr 3.0 separates provider and artifact dependency cliffs into four leaf packages. The root `Mailozaurr` package remains the easiest installation path and contains `Mailozaurr.dll` with reusable profiles, routing, drafts, queues, message actions, and provider composition workflows.

## Package selection

| If the application uses | Reference |
| --- | --- |
| All Mailozaurr library features | `Mailozaurr` |
| SMTP, IMAP, POP3, MIME, SendGrid, Mailgun, or SES | `Mailozaurr.Internet` |
| Microsoft Graph mail | `Mailozaurr.MicrosoftGraph` |
| Gmail API mail | `Mailozaurr.Gmail` |
| OfficeIMO.Email and MimeKit artifact interoperability | `Mailozaurr.Artifacts` |

Graph and Gmail reference Internet. Artifacts is independent of Internet and references only MimeKit and OfficeIMO.Email. OfficeIMO.Security is deliberately not transitive; add it explicitly and pass an `IOfficeSecurityProvider` when C# code needs artifact verification or decryption.

Most reusable types keep the `Mailozaurr` namespace, so an application that references the root package normally needs no `using` changes. Assembly-qualified names, reflection, plugin allow-lists, and linker descriptors must account for transport and provider types moving to their leaf assemblies while workflow types remain in `Mailozaurr.dll`.

## Intentional API moves

- Google token acquisition moved from the mixed `OAuthHelpers` type to `GmailOAuthHelpers` in `Mailozaurr.Gmail`.
- Graph- and Gmail-specific report search moved from `MailboxSearcher` to `GraphMailboxSearcher` and `GmailMailboxSearcher`.
- Provider-native sent-item and threading operations moved to `GraphNativeMailboxOperations` and `GmailNativeMailboxOperations`. Provider-neutral normalization remains on `NativeSentMailboxOperations` and `NativeMailboxThreadingMetadataOperations` in Internet.
- Graph-specific `EmailMessageContent` projection is supplied by the Graph package.
- Graph send methods now return `GraphSmtpResult`, which derives from `SmtpResult` and owns the `GraphError` property in `Mailozaurr.MicrosoftGraph`. Success, dry-run, retry, batch, draft, failure, and SMTP-fallback paths preserve that provider-specific result contract. Common SMTP results no longer expose Graph-specific DTOs.
- Mail-file APIs, including `MailFileReader`, `MailFileMessage`, and `MailFileMimeAdapter`, are supplied by `Mailozaurr.Artifacts`.
- The unpublished `Mailozaurr.Application` project and namespace were removed. Its reusable profiles, drafts, queues, routing, and safety workflows now belong to the public root `Mailozaurr` assembly and namespace; CLI, MCP, and PowerShell consume that owner rather than a separate Host package.

## Artifact and MIME bridge

`MailFileMimeAdapter` supports both directions:

```csharp
using Mailozaurr;
using MimeKit;
using OfficeIMO.Email;

EmailDocument document = EmailDocument.Load("message.msg");
MailFileMimeMessageConversionResult mimeResult =
    await MailFileMimeAdapter.ConvertToMimeMessageAsync(document);

MimeMessage message = mimeResult.Message
    ?? throw new InvalidDataException("The artifact could not be converted to MIME.");

using MailFileEmailDocumentConversionResult documentResult =
    await MailFileMimeAdapter.ConvertToEmailDocumentAsync(message);

Console.WriteLine(documentResult.Document.Subject);
foreach (EmailDiagnostic diagnostic in documentResult.Diagnostics) {
    Console.WriteLine($"{diagnostic.Severity}: {diagnostic.Code} - {diagnostic.Message}");
}
```

The reverse conversion result is disposable because its `EmailDocument` may retain temporary file-backed attachment content. Advanced results retain preservation and conversion diagnostics; convenience methods may throw when conversion reports errors.

## PowerShell

The PSGallery module remains a single installation and composes Internet, Graph, Gmail, Artifacts, and OfficeIMO.Security internally. Package selection and assembly registration are C# concerns; normal PowerShell users continue to install `Mailozaurr` and use the same cmdlet surface.
