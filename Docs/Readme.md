---
Module Name: Mailozaurr
Module Guid: 2b0ea9f1-3ff1-4300-b939-106d5da608fa
Download Help Link: https://github.com/EvotecIT/MailoZaurr
Help Version: 2.1.7
Locale: en-US
---
# Mailozaurr Module
## Description
PowerShell email toolkit for SMTP, IMAP, POP3, Microsoft Graph, Gmail, SendGrid, Mailgun, and Amazon SES, with message-file, PST/OST archive, signing, and encryption workflows.

## Mailozaurr Cmdlets
### [Add-GraphMailboxPermission](Add-GraphMailboxPermission.md)
Adds mailbox permissions via Microsoft Graph.

### [Clear-GraphJunk](Clear-GraphJunk.md)
Clears the Junk Email folder via Microsoft Graph.

### [Clear-IMAPJunk](Clear-IMAPJunk.md)
Clears messages from an IMAP junk folder.

### [Clear-SmtpConnectionPool](Clear-SmtpConnectionPool.md)
Clears all cached SMTP connections used for connection pooling.

The Clear-SmtpConnectionPool cmdlet removes any
pooled SMTP connections maintained by the Smtp class. Use this
when you want to force new connections, for example after changing credentials
or server settings.

### [Close-MailData](Close-MailData.md)
Closes an imported email-data artifact and its owned resources.

Disposes the result returned by Import-MailData, closing PST/OST/OLM/Mbox/mailbox-directory or OAB sessions and releasing file-backed email attachment content.

### [Connect-EmailGraph](Connect-EmailGraph.md)
Connects to Microsoft Graph using application credentials, certificates, device code, or on-behalf-of authentication.

The Connect-EmailGraph cmdlet creates a Microsoft Graph connection for Mailozaurr cmdlets. It supports client secret, certificate, device code, and on-behalf-of flows, and can authenticate directly or from a prebuilt PSCredential.

For new scripts, prefer the SecureString or SecretManagement parameter sets instead of passing secrets and tokens in plain text.

### [Connect-IMAP](Connect-IMAP.md)
Connects to an IMAP server and authenticates using credentials, OAuth2, or clear text.

The Connect-IMAP cmdlet establishes a connection to an IMAP server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns an ImapConnectionInfo object containing connection details and the authenticated client for further use in subsequent cmdlets.

Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable IMAP connectivity in PowerShell automation scenarios.

### [Connect-OAuthGoogle](Connect-OAuthGoogle.md)
Obtains an OAuth2 access token for a Google (Gmail) account for use with IMAP, SMTP, or other Google APIs.

The Connect-OAuthGoogle cmdlet initiates an interactive OAuth2 authentication flow for a Gmail account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP) that support OAuth2 authentication.

For new scripts, prefer -ClientSecretSecureString or -ClientSecretSecretName rather than passing the client secret as plain text.

### [Connect-OAuthO365](Connect-OAuthO365.md)
Obtains an OAuth2 access token for an Office 365 (Microsoft 365) account for use with IMAP, SMTP, or Microsoft Graph.

The Connect-OAuthO365 cmdlet initiates an interactive OAuth2 authentication flow for an Office 365 account, returning a PSCredential object containing the access token. This credential can be used with other cmdlets (such as Connect-IMAP or Send-EmailMessage) that support OAuth2 authentication.

### [Connect-POP3](Connect-POP3.md)
Connects to a POP3 server and authenticates using credentials, OAuth2, or clear text.

The Connect-POP3 cmdlet establishes a connection to a POP3 server using MailKit. It supports multiple authentication methods, including OAuth2, PSCredential, and clear text username/password. The cmdlet returns a PopConnectionInfo object containing connection details and the authenticated client for further use in subsequent cmdlets.

Supports advanced options such as certificate validation skipping, custom timeouts, and secure socket options. Designed for secure, flexible, and scriptable POP3 connectivity in PowerShell automation scenarios.

### [ConvertFrom-EmlToMsg](ConvertFrom-EmlToMsg.md)
Converts EML files to MSG format for compatibility with Microsoft Outlook and other clients.

The ConvertFrom-EmlToMsg cmdlet converts one or more EML files to MSG format. Specify the input EML file paths and the output folder. The cmdlet processes each EML file and saves the converted MSG file in the specified output folder. Supports overwriting existing files with the -Force parameter.

### [ConvertFrom-MsgToEml](ConvertFrom-MsgToEml.md)
Converts MSG files to EML format for interoperability with other clients.

The ConvertFrom-MsgToEml cmdlet converts one or more MSG files to EML format. Provide input MSG file paths and the destination folder. Existing files can be overwritten with the -Force switch.

### [ConvertFrom-OAuth2Credential](ConvertFrom-OAuth2Credential.md)
Extracts username and token from an OAuth2 PSCredential.

The ConvertFrom-OAuth2Credential cmdlet converts a PSCredential containing an OAuth2 access token into a PSCustomObject with UserName and Token properties.

### [ConvertTo-GraphCertificateCredential](ConvertTo-GraphCertificateCredential.md)
Creates a PSCredential containing a Microsoft Graph access token obtained using certificate authentication.

The ConvertTo-GraphCertificateCredential cmdlet authenticates with Microsoft Graph using a client certificate and returns a PSCredential with the access token. Use the resulting credential with cmdlets that accept Graph tokens.

For new scripts, prefer -CertificatePasswordSecureString or -SecretName rather than passing the certificate password as plain text.

### [ConvertTo-GraphCredential](ConvertTo-GraphCredential.md)
Creates a PSCredential object for Microsoft Graph authentication from client ID, secret, and directory ID.

The ConvertTo-GraphCredential cmdlet creates a PSCredential object suitable for Microsoft Graph authentication, using the provided client ID, client secret, and directory (tenant) ID. The resulting credential can be used with cmdlets that require Graph authentication.

For new scripts, prefer -ClientSecretSecureString or -SecretName instead of passing the client secret in plain text.

### [ConvertTo-MailgunCredential](ConvertTo-MailgunCredential.md)
Creates a PSCredential object for Mailgun API authentication from an API key.

Use ConvertTo-MailgunCredential to generate a PSCredential that can be passed to Send-EmailMessage when using the Mailgun provider.

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

### [ConvertTo-MailPst](ConvertTo-MailPst.md)
Converts a supported mail store into a new Unicode PST.

Converts PST, supported PST-compatible OST, OLM, Mbox, EMLX, EML, Maildir, Apple Mail, or EML directory sources through OfficeIMO.Email. The source is never modified and semantic verification is enabled by default.

### [ConvertTo-OAuth2Credential](ConvertTo-OAuth2Credential.md)
Creates a PSCredential object for OAuth2 authentication from a username and token.

The ConvertTo-OAuth2Credential cmdlet creates a PSCredential object suitable for OAuth2 authentication, using the provided username and access token. The resulting credential can be used with cmdlets that require OAuth2 authentication (such as IMAP, SMTP, or POP3 with OAuth2).

For automation and interactive use, prefer -TokenSecureString or -SecretName over passing the token as plain text.

### [ConvertTo-SendGridCredential](ConvertTo-SendGridCredential.md)
Creates a PSCredential object for SendGrid API authentication from an API key.

The ConvertTo-SendGridCredential cmdlet creates a PSCredential object suitable for SendGrid API authentication, using the provided API key. The resulting credential can be used with cmdlets that require SendGrid authentication (such as Send-EmailMessage with the -SendGrid switch).

For new scripts, prefer -ApiKeySecureString or -SecretName instead of passing the API key as plain text.

### [Disconnect-EmailGraph](Disconnect-EmailGraph.md)
Clears Microsoft Graph credentials created by Connect-EmailGraph.

The Disconnect-EmailGraph cmdlet removes sensitive
information from a GraphConnectionInfo object returned by
Connect-EmailGraph. Use it when you no longer need the connection to
ensure credentials are disposed and not kept in memory.

### [Disconnect-IMAP](Disconnect-IMAP.md)
Disconnects an active IMAP connection previously established with Connect-IMAP.

The Disconnect-IMAP cmdlet disconnects an active MailKit IMAP client session. Pass the ImapConnectionInfo object returned by Connect-IMAP to this cmdlet to safely close the connection and release resources.

### [Disconnect-POP3](Disconnect-POP3.md)
Disconnects an active POP3 connection previously established with Connect-POP3.

The Disconnect-POP3 cmdlet disconnects an active MailKit POP3 client session. Pass the PopConnectionInfo object returned by Connect-POP3 to this cmdlet to safely close the connection and release resources.

### [Export-MailFile](Export-MailFile.md)
Exports an imported mail file as EML, MSG, or TNEF.

Writes a MailFileMessage to a file and disposes the input after an attempted export unless -KeepInputOpen is specified. Previewed or rejected operations leave the input open. The destination extension selects EML, MSG, or TNEF, so the same command handles conversion in either direction.

### [Export-MailStore](Export-MailStore.md)
Exports selected items from a PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Delegates to OfficeIMO.Email for EML, MSG, OFT, TNEF, Mbox, Maildir, or EMLX output. The source store remains read-only and the native preservation report is returned.

### [Get-DmarcReport](Get-DmarcReport.md)
Searches for DMARC aggregate reports in a mailbox.

The Get-DmarcReport cmdlet queries IMAP, POP3, Microsoft Graph, or Gmail API to find DMARC aggregate reports and expose zipped XML attachments.

### [Get-EmailDeliveryMatch](Get-EmailDeliveryMatch.md)
Searches for non-delivery reports and matches them to sent messages.

The Get-EmailDeliveryMatch cmdlet searches for non-delivery reports and uses a SendLogResolver to correlate them with sent messages.

### [Get-EmailDeliveryStatus](Get-EmailDeliveryStatus.md)
Searches for non-delivery reports in a mailbox.

The Get-EmailDeliveryStatus cmdlet queries IMAP, POP3, Microsoft Graph, or Gmail API to find non-delivery reports using optional filters.

### [Get-EmailGraphFolder](Get-EmailGraphFolder.md)
Retrieves mail folders for a user via Microsoft Graph API.

The Get-EmailGraphFolder cmdlet retrieves mail folders for the specified user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

### [Get-EmailGraphMessage](Get-EmailGraphMessage.md)
Retrieves mail messages for a user via Microsoft Graph.

The Get-EmailGraphMessage cmdlet fetches messages for the specified user principal name using Microsoft Graph. It supports optional filters like subject, sender, recipient, priority and date range. Results can be limited and optionally deleted.

### [Get-EmailGraphMessageAttachment](Get-EmailGraphMessageAttachment.md)
Retrieves attachments for a specific mail message via Microsoft Graph API.

The Get-EmailGraphMessageAttachment cmdlet retrieves attachments for the specified mail message ID and user principal name using Microsoft Graph API. Provide a GraphConnectionInfo object created with Connect-EmailGraph or authenticate via Connect-MgGraph.

### [Get-EmailGraphMessageMime](Get-EmailGraphMessageMime.md)
Retrieves a MIME representation of a Graph mail message.

### [Get-EmailPendingMessage](Get-EmailPendingMessage.md)
Retrieves pending email messages from a file based repository.

### [Get-GmailMessage](Get-GmailMessage.md)
Retrieves messages using the Gmail API.

### [Get-GmailThread](Get-GmailThread.md)
Retrieves Gmail threads using the Gmail API.

### [Get-GraphEvent](Get-GraphEvent.md)
Retrieves calendar events using Microsoft Graph.

### [Get-GraphInboxRule](Get-GraphInboxRule.md)
Retrieves inbox rules for a mailbox via Microsoft Graph.

### [Get-GraphMailboxPermission](Get-GraphMailboxPermission.md)
Retrieves mailbox permissions for a user via Microsoft Graph.

### [Get-GraphMailboxStatistics](Get-GraphMailboxStatistics.md)
Retrieves detailed mailbox statistics via Microsoft Graph, including
message and folder counts as well as attachment sizes.

### [Get-IMAPFolder](Get-IMAPFolder.md)
Retrieves the IMAP inbox folder and updates message counts for an active IMAP connection.

The Get-IMAPFolder cmdlet opens the inbox folder for the provided ImapConnectionInfo object (from Connect-IMAP), updates message and recent counts, and returns the updated connection info. Use this to refresh folder state or after connecting to an IMAP server.

### [Get-IMAPMessage](Get-IMAPMessage.md)
Retrieves messages from an IMAP folder using optional filters.

The Get-IMAPMessage cmdlet fetches messages from the current IMAP folder associated with the provided ImapConnectionInfo object. You can filter by subject, sender, recipients, priority, date range and attachment presence. Messages can also be deleted after retrieval.

### [Get-MailStoreFolder](Get-MailStoreFolder.md)
Lists folders from an imported PST, OST, OLM, Mbox, EMLX, or mailbox directory.

Returns OfficeIMO.Email folder metadata without decoding message bodies or attachments.

### [Get-MailStoreItem](Get-MailStoreItem.md)
Enumerates or reads items from an imported mail store.

Returns lightweight OfficeIMO.Email item references by default. Use Read to project selected message parts while the owning store remains open.

### [Get-MimeMessageContent](Get-MimeMessageContent.md)
Retrieves text and HTML bodies from a MIME message.

### [Get-POP3Message](Get-POP3Message.md)
Retrieves messages from a POP3 mailbox with optional filters.

The Get-POP3Message cmdlet fetches messages from a POP3 mailbox using the provided PopConnectionInfo object. It supports filtering by subject, sender, recipients, priority, date range and attachment presence. Messages can be removed after retrieval using -Delete.

### [Get-SmtpConnectionPool](Get-SmtpConnectionPool.md)
Retrieves information about the SMTP connection pool.

The Get-SmtpConnectionPool cmdlet returns a snapshot of
pooled SMTP connections or, when used with -Watch, continuously emits
updates as the pool changes. An optional -Action script block can be
executed for each update.

### [Import-MailData](Import-MailData.md)
Opens an email-data artifact through its OfficeIMO.Email owner.

Detects EML, MSG, OFT, TNEF, ICS, VCF, PST, OST, OLM, EMLX, Mbox, Maildir, Apple Mail directories, and Outlook Offline Address Book data. The returned owner result must be closed when it contains a store, address-book session, or streaming email content.

### [Import-MailFile](Import-MailFile.md)
Imports an EML, MSG, OFT, or TNEF mail file and returns its contents as a message object.

The Import-MailFile cmdlet loads a native mail artifact and returns a MailFileMessage for further processing, inspection, or conversion.

### [Merge-MailStore](Merge-MailStore.md)
Merges multiple read-only mail stores into a new Unicode PST.

Delegates folder mapping, bounded retries, semantic deduplication, and PST writing to OfficeIMO.Email. Source PST, OST, OLM, EMLX, Mbox, and mailbox-directory data is never modified.

### [Move-GraphFolder](Move-GraphFolder.md)
Moves a Microsoft Graph mail folder.

### [Move-GraphMessage](Move-GraphMessage.md)
Moves a Microsoft Graph message to a different folder.

### [Move-IMAPFolder](Move-IMAPFolder.md)
Moves an IMAP folder to a new location.

### [Move-IMAPMessage](Move-IMAPMessage.md)
Moves an IMAP message to another folder.

The Move-IMAPMessage cmdlet moves a message identified by its UID from the current folder to the specified destination folder.

### [New-EmailAttachment](New-EmailAttachment.md)
Creates an attachment descriptor for Mailozaurr send cmdlets.

### [New-GraphEvent](New-GraphEvent.md)
Creates a new calendar event via Microsoft Graph.

### [New-GraphEventBuilder](New-GraphEventBuilder.md)
Creates a GraphEventBuilder instance.

### [New-GraphInboxRule](New-GraphInboxRule.md)
Creates a new inbox rule via Microsoft Graph.

### [New-GraphInboxRuleBuilder](New-GraphInboxRuleBuilder.md)
Creates a GraphInboxRuleBuilder instance.

### [New-GraphInboxRuleObject](New-GraphInboxRuleObject.md)
Creates a GraphInboxRule object.

### [New-GraphMailboxPermissionBuilder](New-GraphMailboxPermissionBuilder.md)
Creates a GraphMailboxPermissionBuilder instance.

### [New-GraphMailboxPermissionObject](New-GraphMailboxPermissionObject.md)
Creates a GraphMailboxPermission object.

### [New-IMAPSearchQuery](New-IMAPSearchQuery.md)
Creates a Mailozaurr IMAP search query without requiring callers to construct MailKit types directly.

### [New-MimeMessage](New-MimeMessage.md)
Creates a MIME message without requiring callers to construct MimeKit types directly.

### [New-TemporaryMailCrypto](New-TemporaryMailCrypto.md)
Creates temporary cryptographic material for testing mail encryption.

### [Remove-EmailPendingMessage](Remove-EmailPendingMessage.md)
Removes pending messages from a file based repository.

### [Remove-GmailMessage](Remove-GmailMessage.md)
Deletes a Gmail message.

### [Remove-GraphEvent](Remove-GraphEvent.md)
Removes a calendar event via Microsoft Graph.

### [Remove-GraphFolder](Remove-GraphFolder.md)
Removes a Microsoft Graph mail folder.

### [Remove-GraphInboxRule](Remove-GraphInboxRule.md)
Removes an inbox rule via Microsoft Graph.

### [Remove-GraphMailboxPermission](Remove-GraphMailboxPermission.md)
Removes mailbox permissions via Microsoft Graph.

### [Remove-GraphMessage](Remove-GraphMessage.md)
Deletes a message from Microsoft Graph.

### [Remove-GraphMessageAttachment](Remove-GraphMessageAttachment.md)
Removes attachments from a GraphMessage instance.

### [Remove-IMAPFolder](Remove-IMAPFolder.md)
Removes an IMAP folder.

### [Remove-IMAPMessage](Remove-IMAPMessage.md)
Removes messages from an IMAP folder by UID.

### [Remove-IMAPMessageAttachment](Remove-IMAPMessageAttachment.md)
Removes attachments from an IMAP MimeMessage instance.

### [Remove-POP3Message](Remove-POP3Message.md)
Removes messages from a POP3 mailbox by index.

### [Remove-POP3MessageAttachment](Remove-POP3MessageAttachment.md)
Removes attachments from a POP3 MimeMessage instance.

### [Rename-GraphFolder](Rename-GraphFolder.md)
Renames a Microsoft Graph mail folder.

### [Rename-IMAPFolder](Rename-IMAPFolder.md)
Renames an IMAP folder.

### [Save-GmailMessageAttachment](Save-GmailMessageAttachment.md)
Saves attachments from a Gmail message to disk.

### [Save-GraphMessage](Save-GraphMessage.md)
Saves Microsoft Graph email messages to disk in a specified format.

The Save-GraphMessage cmdlet saves one or more EmailGraphMessage objects to disk at the specified path. Use this to archive, export, or process messages retrieved from Microsoft Graph.

### [Save-GraphMessageAttachment](Save-GraphMessageAttachment.md)
Saves attachments from Microsoft Graph message objects.

### [Save-IMAPMessage](Save-IMAPMessage.md)
Saves an IMAP message to disk at the specified path.

The Save-IMAPMessage cmdlet saves a message from an IMAP mailbox (using a ImapConnectionInfo object from Connect-IMAP) to disk at the given path. Provide the unique identifier of the message to export or archive it.

### [Save-IMAPMessageAttachment](Save-IMAPMessageAttachment.md)
Saves attachments from an IMAP message to disk.

The Save-IMAPMessageAttachment cmdlet saves all attachments from an IMAP message identified by its UID to the specified directory.

### [Save-MimeMessage](Save-MimeMessage.md)
Saves a MIME message or wrapper object to disk.

### [Save-POP3Message](Save-POP3Message.md)
Saves a POP3 message to disk in either EML or MSG format.

The Save-POP3Message cmdlet saves a message from a POP3 mailbox (using a PopConnectionInfo object from Connect-POP3) to disk at the specified path. Use this to archive, export, or process messages retrieved from a POP3 server. The message can be saved as an EML file or converted to MSG format.

### [Save-POP3MessageAttachment](Save-POP3MessageAttachment.md)
Saves attachments from a POP3 message to disk.

The Save-POP3MessageAttachment cmdlet saves all attachments from a POP3 message identified by its index to the specified directory.

### [Search-GraphMailbox](Search-GraphMailbox.md)
Searches one or more mailboxes using Microsoft Graph.

The Search-GraphMailbox cmdlet queries Microsoft Graph using application permissions. Provide multiple user principal names to search across several mailboxes. Results are returned as GraphMessageInfo objects.

### [Search-IMAPMailbox](Search-IMAPMailbox.md)
Searches an IMAP mailbox and returns matching messages.

### [Search-MailStore](Search-MailStore.md)
Searches mail-store metadata or selected message content.

Uses OfficeIMO.Email bounded summary search by default. Supplying Term enables resumable content search across selected semantic fields.

### [Search-POP3Mailbox](Search-POP3Mailbox.md)
Searches a POP3 mailbox and returns matching messages.

### [Send-EmailMessage](Send-EmailMessage.md)
Sends an email message using SMTP, SendGrid, or Microsoft Graph from within PowerShell. Replaces the deprecated Send-MailMessage.

The Send-EmailMessage cmdlet sends an email message using a variety of providers and authentication methods. It supports SMTP (with or without SSL/TLS), SendGrid API, and Microsoft Graph API. The cmdlet allows for rich email composition, including HTML and text bodies, attachments, delivery notifications, and advanced logging. It is designed as a modern, secure, and flexible replacement for Send-MailMessage.

Authentication can be provided via credentials, OAuth2, or provider-specific tokens. The cmdlet supports multiple parameter sets for compatibility with different authentication and provider scenarios.

### [Send-EmailPendingMessage](Send-EmailPendingMessage.md)
Sends pending messages stored in a file-based repository.

### [Send-GmailMessage](Send-GmailMessage.md)
Sends an email using the Gmail API.

### [Set-GraphEvent](Set-GraphEvent.md)
Updates an existing calendar event via Microsoft Graph.

### [Set-GraphInboxRule](Set-GraphInboxRule.md)
Updates an existing inbox rule via Microsoft Graph.

### [Set-GraphMessage](Set-GraphMessage.md)
Updates properties of an existing Microsoft Graph message.

### [Set-IMAPFolder](Set-IMAPFolder.md)
Sets the working IMAP folder for subsequent operations.

### [Set-IMAPMessage](Set-IMAPMessage.md)
Updates flags on an IMAP message.

### [Set-POP3Message](Set-POP3Message.md)
Updates local flags on a POP3 message.

### [Test-EmailAddress](Test-EmailAddress.md)
Validates one or more email addresses for format and standards compliance.

The Test-EmailAddress cmdlet checks if one or more email addresses are valid according to standard email address rules. Supports validation for international addresses and top-level domains. Returns validation results for each address.

### [Test-MailStore](Test-MailStore.md)
Runs bounded validation against an imported mail store.

Validates PST, OST, OLM, EMLX, Mbox, or mailbox-directory data at shallow, summary, or full-item depth. Structural PST/OST page and block verification is opt-in.

### [Test-MimeMessageSignature](Test-MimeMessageSignature.md)
Verifies PGP or S/MIME signatures on a MimeMessage.

### [Test-SmtpConnection](Test-SmtpConnection.md)
Tests SMTP connectivity and reports server capabilities.

The Test-SmtpConnection cmdlet connects to an
SMTP server and returns information about supported features. It also checks
if the connection remains open after a NOOP command which indicates support
for persistent connections. It can also perform an envelope-only recipient
probe or send an explicit validation message for authorized mail-flow testing.

### [Unprotect-MimeMessage](Unprotect-MimeMessage.md)
Decrypts an encrypted MimeMessage using PGP or S/MIME.

### [Wait-GraphMessage](Wait-GraphMessage.md)
Waits for new Graph messages by polling Microsoft Graph.

The Wait-GraphMessage cmdlet listens for new messages for the specified user principal name. Messages are written to the pipeline as they arrive.

### [Wait-IMAPMessage](Wait-IMAPMessage.md)
Waits for new IMAP messages using the IMAP IDLE command.

The Wait-IMAPMessage cmdlet listens for new messages arriving in the specified folder and writes them to the pipeline as they are received.

### [Wait-POP3Message](Wait-POP3Message.md)
Waits for new POP3 messages by polling the server.

The Wait-POP3Message cmdlet listens for new messages arriving in the connected POP3 mailbox. Messages are written to the pipeline as they are received.

### [Watch-SmtpConnectionPool](Watch-SmtpConnectionPool.md)
Subscribes to SMTP connection pool updates.

The Watch-SmtpConnectionPool cmdlet registers a handler
that executes a provided script block whenever the SMTP connection pool
changes. The handler is returned so it can be removed when no longer
needed.
