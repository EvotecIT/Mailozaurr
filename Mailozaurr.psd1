@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Add-GraphMailboxPermission', 'Clear-GraphJunk', 'Clear-IMAPJunk', 'Connect-EmailGraph', 'Connect-IMAP', 'Connect-OAuthGoogle', 'Connect-OAuthO365', 'Connect-POP3', 'ConvertFrom-EmlToMsg', 'ConvertFrom-MsgToEml', 'ConvertFrom-OAuth2Credential', 'ConvertTo-GraphCertificateCredential', 'ConvertTo-GraphCredential', 'ConvertTo-MailgunCredential', 'ConvertTo-OAuth2Credential', 'ConvertTo-SendGridCredential', 'Disconnect-EmailGraph', 'Disconnect-IMAP', 'Disconnect-POP3', 'Get-EmailGraphFolder', 'Get-EmailGraphMessage', 'Get-EmailGraphMessageAttachment', 'Get-GraphInboxRule', 'Get-GraphMailboxPermission', 'Get-GraphMailboxStatistics', 'Get-GmailMessage', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-POP3Message', 'Import-MailFile', 'Move-GraphFolder', 'Move-GraphMessage', 'Move-IMAPFolder', 'Move-IMAPMessage', 'New-GraphInboxRule', 'New-GraphInboxRuleBuilder', 'New-GraphInboxRuleObject', 'New-GraphMailboxPermissionBuilder', 'New-GraphMailboxPermissionObject', 'Remove-GraphFolder', 'Remove-GraphInboxRule', 'Remove-GraphMailboxPermission', 'Remove-GraphMessage', 'Remove-IMAPFolder', 'Remove-IMAPMessage', 'Remove-POP3Message', 'Rename-GraphFolder', 'Rename-IMAPFolder', 'Save-GraphMessage', 'Save-GraphMessageAttachment', 'Save-IMAPMessage', 'Save-IMAPMessageAttachment', 'Save-POP3Message', 'Save-POP3MessageAttachment', 'Search-GraphMailbox', 'Search-IMAPMailbox', 'Search-POP3Mailbox', 'Send-EmailMessage', 'Send-GmailMessage', 'Remove-GmailMessage', 'Save-GmailMessageAttachment', 'Set-GraphInboxRule', 'Set-GraphMessage', 'Set-IMAPFolder', 'Set-IMAPMessage', 'Set-POP3Message', 'Test-EmailAddress', 'Wait-GraphMessage', 'Wait-IMAPMessage', 'Wait-POP3Message')
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
            Prerelease = 'Preview7'
            ProjectUri = 'https://github.com/EvotecIT/MailoZaurr'
            Tags       = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3')
        }
    }
    RootModule           = 'Mailozaurr.psm1'
}
