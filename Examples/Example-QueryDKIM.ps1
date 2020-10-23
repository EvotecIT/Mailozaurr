Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

# Https way via Cloudflare
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

# Https way via Google
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -Selector 'selector1' -DNSProvider Google | Format-Table *