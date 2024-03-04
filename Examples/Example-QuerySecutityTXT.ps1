Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-SecurityTxtRecord -DomainName 'evotec.xyz', 'evotec.pl', 'google.com', 'facebook.com', 'www.gemini.com' | Format-Table -AutoSize *