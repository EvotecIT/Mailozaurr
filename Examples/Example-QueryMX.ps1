Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Standard way
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

# Https way via Cloudflare
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

# Https way via Google
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *

# Standard way with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -ResolvePTR | Format-Table *

# Https way via Cloudflare with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare -ResolvePTR | Format-Table *

# Https way via Google with ResolvePTR
Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google -ResolvePTR | Format-Table *