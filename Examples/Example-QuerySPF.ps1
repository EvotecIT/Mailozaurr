Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

# Https way via Cloudflare
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

# Https way via Google
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *