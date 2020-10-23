Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

# Https way via Cloudflare
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

# Https way via Google
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *