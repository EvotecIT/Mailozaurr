Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# need to verify - cloudflare occassionally fails
Find-DANERecord -DomainName 'ietf.org' -DNSProvider Google | Format-Table
Find-DANERecord -DomainName 'ietf.org' -DNSProvider Cloudflare | Format-Table
Find-DANERecord -DomainName 'ietf.org' | Format-Table