Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Key = Get-Content -Raw -Path "C:\Support\Important\SendGrid.txt"

# Use SendGrid via Standard SMTP
# username needs to be named exactly apikey
Send-EmailMessage -From 'przemyslaw.klys@evo.cool' -To 'przemyslaw.klys@evotec.pl', 'evotectest@gmail.com' `
    -Username 'apikey' `
    -Server 'smtp.sendgrid.net' `
    -Password $Key `
    -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' -DeliveryNotificationOption OnSuccess `
    -Priority High -Subject '😒💖 This is another test email 我' -UseSsl -Port 587 -Verbose #-WhatIf