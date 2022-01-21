Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}

$Text = Get-Content -Path "$PSScriptRoot\Input\Test.txt" -Raw

# this is simple replacement (drag & drop to Send-MailMessage)
Send-EmailMessage -To 'przemyslaw.klys@test.pl' -Subject 'Test' -Text $Text -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@test.pl' -Priority High -Credential $MailCredentials -UseSsl -Port 587 -Verbose