Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# https://bimigroup.org/bimi-generator/
# need to verify - cloudflare occassionally fails
Find-BIMIRecord -DomainName 'mxtoolbox.com' -DNSProvider Google | Format-Table
Find-BIMIRecord -DomainName 'google.com' -DNSProvider Cloudflare | Format-Table
Find-BIMIRecord -DomainName 'mxtoolbox.com' | Format-Table