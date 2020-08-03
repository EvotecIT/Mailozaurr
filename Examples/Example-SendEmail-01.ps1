Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}
# this is simple replacement (drag & drop to Send-MailMessage)
Send-EmailMessage -To 'przemyslaw.klys@test.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@test.pl' `
    -Attachments "$PSScriptRoot\..\README.MD", "$PSScriptRoot\..\Mailozaurr.psm1" -Encoding UTF8 -Cc 'przemyslaw.klys@test.pl' -Priority High -Credential $MailCredentials `
    -UseSsl -Port 587 -Verbose

$Body = EmailBody {
    EmailText -Text 'This is my text'
    EmailTable -DataTable (Get-Process | Select-Object -First 5 -Property Name, Id, PriorityClass, CPU, Product)
}
$Text = 'This is my text'

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test.pl' } -To 'przemyslaw.klys@test.pl' `
    -Server 'smtp.office365.com' -Credential $MailCredentials -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'This is another test email' -SecureSocketOptions Auto