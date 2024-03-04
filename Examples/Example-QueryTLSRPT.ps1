Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# need to verify - cloudflare occassionally fails
Find-TLSRPTRecord -DomainName 'evotec.xyz' -DNSProvider Cloudflare
Find-TLSRPTRecord -DomainName 'evotec.xyz' -DNSProvider Google
Find-TLSRPTRecord -DomainName 'evotec.xyz'