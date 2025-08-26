@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Add-GraphMailboxPermission', 'Clear-GraphJunk', 'Clear-IMAPJunk', 'Clear-SmtpConnectionPool', 'Connect-EmailGraph', 'Connect-IMAP', 'Connect-OAuthGoogle', 'Connect-OAuthO365', 'Connect-POP3', 'ConvertFrom-EmlToMsg', 'ConvertFrom-MsgToEml', 'ConvertFrom-OAuth2Credential', 'ConvertTo-GraphCertificateCredential', 'ConvertTo-GraphCredential', 'ConvertTo-MailgunCredential', 'ConvertTo-OAuth2Credential', 'ConvertTo-SendGridCredential', 'Disconnect-EmailGraph', 'Disconnect-IMAP', 'Disconnect-POP3', 'Get-DmarcReport', 'Get-EmailDeliveryMatch', 'Get-EmailDeliveryStatus', 'Get-EmailGraphFolder', 'Get-EmailGraphMessage', 'Get-EmailGraphMessageAttachment', 'Get-EmailGraphMessageMime', 'Get-GmailMessage', 'Get-GmailThread', 'Get-GraphEvent', 'Get-GraphInboxRule', 'Get-GraphMailboxPermission', 'Get-GraphMailboxStatistics', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-MimeMessageContent', 'Get-POP3Message', 'Get-SmtpConnectionPool', 'Import-MailFile', 'Move-GraphFolder', 'Move-GraphMessage', 'Move-IMAPFolder', 'Move-IMAPMessage', 'New-GraphEvent', 'New-GraphEventBuilder', 'New-GraphInboxRule', 'New-GraphInboxRuleBuilder', 'New-GraphInboxRuleObject', 'New-GraphMailboxPermissionBuilder', 'New-GraphMailboxPermissionObject', 'New-TemporaryMailCrypto', 'Remove-GmailMessage', 'Remove-GraphEvent', 'Remove-GraphFolder', 'Remove-GraphInboxRule', 'Remove-GraphMailboxPermission', 'Remove-GraphMessage', 'Remove-GraphMessageAttachment', 'Remove-IMAPFolder', 'Remove-IMAPMessage', 'Remove-IMAPMessageAttachment', 'Remove-POP3Message', 'Remove-POP3MessageAttachment', 'Rename-GraphFolder', 'Rename-IMAPFolder', 'Save-GmailMessageAttachment', 'Save-GraphMessage', 'Save-GraphMessageAttachment', 'Save-IMAPMessage', 'Save-IMAPMessageAttachment', 'Save-MimeMessage', 'Save-POP3Message', 'Save-POP3MessageAttachment', 'Search-GraphMailbox', 'Search-IMAPMailbox', 'Search-POP3Mailbox', 'Send-EmailMessage', 'Send-GmailMessage', 'Set-GraphEvent', 'Set-GraphInboxRule', 'Set-GraphMessage', 'Set-IMAPFolder', 'Set-IMAPMessage', 'Set-POP3Message', 'Test-EmailAddress', 'Test-MimeMessageSignature', 'Test-SmtpConnection', 'Unprotect-MimeMessage', 'Wait-GraphMessage', 'Wait-IMAPMessage', 'Wait-POP3Message', 'Watch-SmtpConnectionPool')
    CompanyName          = 'Evotec'
    CompatiblePSEditions = @('Desktop', 'Core')
    Copyright            = '(c) 2011 - 2025 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'Mailozaurr is a PowerShell module that aims to provide SMTP, POP3, IMAP and few other ways to interact with Email. Underneath it uses MimeKit and MailKit and EmailValidation libraries written by Jeffrey Stedfast.            '
    FunctionsToExport    = @()
    GUID                 = '2b0ea9f1-3ff1-4300-b939-106d5da608fa'
    ModuleVersion        = '2.0.0'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            IconUri    = 'https://evotec.xyz/wp-content/uploads/2020/07/MailoZaurr.png'
            Prerelease = 'Preview9'
            ProjectUri = 'https://github.com/EvotecIT/MailoZaurr'
            Tags       = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3')
        }
    }
    RootModule           = 'Mailozaurr.psm1'
}