Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Use SendGrid Api
$Credential = ConvertTo-SendGridCredential -ApiKey 'SG.q'

Send-EmailMessage -From 'przemyslaw.klys@evo.cool' `
    -To 'przemyslaw.klys@evotec.pl', 'evotectest@gmail.com' `
    -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' `
    -Priority High `
    -Subject '😒💖 This is another test email 我' `
    -SendGrid `
    -Credential $Credential `
    -Verbose -WhatIf

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test.pl' } -To 'przemyslaw.klys@test.pl' -Credential $Credential -Text 'MyTest' -Priority High `
    -Subject 'This is another test email' -WhatIf -SendGrid -Verbose