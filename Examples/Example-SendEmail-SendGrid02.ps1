Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Use SendGrid Api
$Key = Read-Host 'SendGrid API key' -AsSecureString

$Credential = ConvertTo-SendGridCredential -ApiKeySecureString $Key

Send-EmailMessage -From 'przemyslaw.klys@evo.cool' `
    -To 'przemyslaw.klys@evotec.pl', 'evotectest@gmail.com' `
    -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' `
    -Priority High `
    -Subject '😒💖 This is another test email 我' `
    -SendGrid `
    -Credential $Credential `
    -Verbose

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@evo.cool' } -To 'przemyslaw.klys@evotec.pl' -Credential $Credential -Text 'MyTest' -Priority High `
    -Subject 'Second test email' -SendGrid -Verbose
