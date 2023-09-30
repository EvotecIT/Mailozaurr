Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# need to verify - cloudflare occassionally fails
Find-MTASTSRecord -DomainName 'google.com' -DNSProvider Google | Format-Table
Find-MTASTSRecord -DomainName 'google.com' -DNSProvider Cloudflare | Format-Table
Find-MTASTSRecord -DomainName 'google.com' | Format-Table
Find-MTASTSRecord -DomainName 'evotec.xyz' | Format-Table