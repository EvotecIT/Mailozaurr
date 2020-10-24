Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Use SendGrid via Standard SMTP
# username needs to be named exactly apikey
Send-EmailMessage -From 'przemyslaw.klys@evo.cool' -To 'przemyslaw.klys@evo.pl', 'evote@Test.com' `
    -Username 'apikey' `
    -Server 'smtp.sendgrid.net' `
    -Password 'SG.fMrU' `
    -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' -DeliveryNotificationOption OnSuccess `
    -Priority High -Subject '😒💖 This is another test email 我' -UseSsl -Port 587 -Verbose