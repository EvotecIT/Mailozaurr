Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-DNSSECRecord -DomainName 'evotec.pl', 'google.com', 'onet.pl' | Format-Table *

# Https way via Cloudflare
Find-DNSSECRecord -DomainName 'evotec.pl', 'google.com', 'onet.pl' -DNSProvider Cloudflare | Format-Table *

# Https way via Google
Find-DNSSECRecord -DomainName 'evotec.pl', 'google.com', 'onet.pl' -DNSProvider Google | Format-Table *