Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Body = EmailBody {
    EmailText -Text "This is my text"
    EmailTable -DataTable (Get-Process | Select-Object -First 5 -Property Name, Id, PriorityClass, CPU, Product)
}

$Text = @"
    This is my text
"@

if (-not $MailCredentials) {
    $MailCredentials = Get-Credential
}
# this is simple replacement (drag & drop to Send-MailMessage)
Send-EmailMessage -To 'przemyslaw.klys@evotec.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@evotec.pl' `
    -Attachments "$PSScriptRoot\..\README.MD", "$PSScriptRoot\..\Mailozaurr.psm1" -Encoding UTF8 -Cc 'przemyslaw.klys@evotec.pl' <#-DeliveryNotificationOption OnSuccess, OnFailure#> `
    -Priority High -Credential $MailCredentials -UseSsl -Port 587 -Verbose -ErrorAction Stop

# this is how you would send it with original
#Send-MailMessage -To 'przemyslaw.klys@evotec.pl' -Subject 'Test' -Body 'test me' -SmtpServer 'smtp.office365.com' -From 'przemyslaw.klys@evotec.pl' `
#    -Attachments "$PSScriptRoot\..\Mailozaurr.psd1" -Encoding UTF8 -Cc 'przemyslaw.klys@evotec.pl'  -DeliveryNotificationOption OnSuccess -Priority High -Credential $MailCredentials -UseSsl -Port 587 -Verbose

Send-EmailMessage -From @{ Name = "Przemysław Kłys"; Email = 'przemyslaw.klys@evotec.pl' } -To 'przemyslaw.klys@evotec.pl' `
    -Server 'smtp.office365.com' -Credential $MailCredentials -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'This is another test email' -SecureSocketOptions Auto