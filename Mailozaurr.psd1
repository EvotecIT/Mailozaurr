@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Connect-EmailGraph', 'Connect-IMAP', 'Connect-OAuthGoogle', 'Connect-OAuthO365', 'Connect-POP3', 'ConvertFrom-EmlToMsg', 'ConvertFrom-MsgToEml', 'ConvertFrom-OAuth2Credential', 'ConvertTo-GraphCertificateCredential', 'ConvertTo-GraphCredential', 'ConvertTo-MailgunCredential', 'ConvertTo-OAuth2Credential', 'ConvertTo-SendGridCredential', 'Disconnect-EmailGraph', 'Disconnect-IMAP', 'Disconnect-POP3', 'Get-EmailGraphMessage', 'Get-IMAPFolder', 'Get-IMAPMessage', 'Get-EmailGraphFolder', 'Get-EmailGraphMessageAttachment', 'Get-POP3Message', 'Import-MailFile', 'Move-GraphMessage', 'Move-IMAPMessage', 'Move-IMAPFolder', 'Move-GraphFolder', 'Rename-IMAPFolder', 'Rename-GraphFolder', 'Remove-IMAPFolder', 'Remove-GraphFolder', 'Save-GraphMessage', 'Save-GraphMessageAttachment', 'Save-IMAPMessage', 'Save-IMAPMessageAttachment', 'Save-POP3Message', 'Save-POP3MessageAttachment', 'Send-EmailMessage', 'Set-GraphMessage', 'Set-IMAPMessage', 'Set-POP3Message', 'Set-IMAPFolder', 'Test-EmailAddress', 'Wait-IMAPMessage', 'Wait-GraphMessage', 'Wait-POP3Message', 'Search-GraphMailbox', 'Remove-GraphMessage', 'Search-IMAPMailbox', 'Search-POP3Mailbox', 'Remove-IMAPMessage', 'Remove-POP3Message', 'Clear-IMAPJunk', 'Clear-GraphJunk', 'Get-GraphInboxRule', 'New-GraphInboxRule', 'Set-GraphInboxRule', 'Remove-GraphInboxRule', 'New-GraphInboxRuleObject')
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
            Prerelease = 'Preview6'
            ProjectUri = 'https://github.com/EvotecIT/MailoZaurr'
            Tags       = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3')
        }
    }
    RootModule           = 'Mailozaurr.psm1'
}