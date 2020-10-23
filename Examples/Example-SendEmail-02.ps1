Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$UserNotify = 'Przemyslaw'

$Body = EmailBody -FontFamily 'Calibri' -Size 15 {
    EmailText -Text 'Hello ', $UserNotify, ',' -Color None, Blue, None -Verbose -LineBreak
    EmailText -Text 'Your password is due to expire in ', $PasswordExpiryDays, 'days.' -Color None, Green, None
    EmailText -LineBreak
    EmailText -Text 'To change your password: '
    EmailText -Text '- press ', 'CTRL+ALT+DEL', ' -> ', 'Change a password...' -Color None, BlueViolet, None, Red
    EmailText -LineBreak
    EmailTextBox {
        'If you have forgotten your password and need to reset it, you can do this by clicking here. '
        'In case of problems please contact the HelpDesk by visiting [Evotec Website](https://evotec.xyz) or by sending an email to Help Desk.'
    }
    EmailText -LineBreak
    EmailText -Text 'Alternatively you can always call ', 'Help Desk', ' at ', '+48 22 00 00 00' `
        -Color None, LightSkyBlue, None, LightSkyBlue -TextDecoration none, underline, none, underline -FontWeight normal, bold, normal, bold
    EmailText -LineBreak
    EmailTextBox {
        'Kind regards,'
        'Evotec IT'
    }
}

if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test.pl' } -To 'przemyslaw.klys@test.pl' `
    -Server 'smtp.office365.com' -SecureSocketOptions Auto -Credential $MailCredentials -HTML $Body -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'This is another test email'

Send-MailMessage -To 'przemyslaw.klys@test.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@test.pl' `
    -Attachments "$PSScriptRoot\..\Mailozaurr.psd1" -Cc 'przemyslaw.klys@test.pl' -DeliveryNotificationOption OnSuccess -Priority High -Credential $MailCredentials -UseSsl -Port 587 -Verbose # -Encoding UTF8
