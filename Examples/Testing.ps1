Import-Module $PSScriptRoot\..\PSMailKit.psd1 -Force


#[MailKit.Net.Imap.ImapClient]

#[MailKit.Net.Smtp.SmtpClient]

#[MimeKit.MailboxAddress]

$Text = [MimeKit.TextPart]::new()
$Text.Text = 'Ooopsie'

$To = [MimeKit.MailboxAddress]::new('przemyslaw.klys@euvic.pl')
$From = [MimeKit.MailboxAddress]::new('przemyslaw.klys@evotec.pl')



$Credentials = Get-Credential -UserName 'przemyslaw.klys@evotec.pl'
#Send-EmailMessage -From 'przemyslaw.klys@evotec.pl' -Server 'smtp.office365.com' -To 'przemyslaw.klys@euvic.pl', 'przemyslaw.klys@evotec.pl' -SecureSocketOptions StartTlsWhenAvailable -Credentials $Credentials


$Body = Email {
    EmailHeader {
        EmailFrom -Address 'reminder@domain.pl'
        EmailTo -Addresses "przemyslaw.klys@domain.pl"
        EmailServer -Server 'mail.evotec.com' -UserName 'YourUsername' -Password 'C:\Support\Important\Password-Evotec-Reminder.txt' -PasswordAsSecure -PasswordFromFile
        EmailOptions -Priority High -DeliveryNotifications Never
        EmailSubject -Subject 'This is a test email'
    }
    EmailBody -FontFamily 'Calibri' -Size 15 {
        EmailTextBox {
            "Hello $UserNotify,"
            ""
            "Your password is due to expire in $PasswordExpiryDays days."
            ""
            'To change your password: '
            '- press CTRL+ALT+DEL -> Change a password...'
            ''
            'If you have forgotten your password and need to reset it, you can do this by clicking here. '
            "In case of problems please contact the HelpDesk by visiting [Evotec Website](https://evotec.xyz) or by sending an email to Help Desk."
            ''
            'Alternatively you can always call Help Desk at +48 22 00 00 00'
            ''
            'Kind regards,'
            'Evotec IT'
        }
        EmailText -LineBreak
    }
} -WhatIf -FilePath $PSScriptRoot\Test.html

$Body = Get-Content $PSScriptRoot\Test.html

Send-EmailMessage -From @{ Name = "Przemysław Kłys"; Email = 'przemyslaw.klys@evotec.pl' } `
    -Server 'smtp.office365.com' -To 'przemyslaw.klys@euvic.pl' `
    -SecureSocketOptions StartTlsWhenAvailable -Credentials $Credentials -HTML $Body -Text $Text


