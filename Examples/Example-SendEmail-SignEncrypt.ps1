Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$CertPath = 'C:\Certificates\email.pfx'
$CertPassword = 'pfx-password'

if (-not $SmtpCredential) {
    $SmtpCredential = Get-Credential
}

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' \
    -Server 'smtp.example.com' -Credential $SmtpCredential -Port 587 -UseSsl \
    -Subject 'Signed and encrypted' -Body 'This message is signed and encrypted.' \
    -CertificatePath $CertPath -CertificatePassword $CertPassword \
    -SignOrEncrypt SMIMESignAndEncrypt -Verbose
