Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Find-AutoDiscoverRecord -DomainName 'evotec.pl' -DNSProvider Google | Format-Table
Find-AutoDiscoverRecord -DomainName 'evotec.pl' -DNSProvider Cloudflare | Format-Table
Find-AutoDiscoverRecord -DomainName 'evotec.pl' | Format-Table

Find-AutoDiscoverRecord -DomainName 'evotec.pl' -DNSProvider Google | Format-Table
Find-AutoDiscoverRecord -DomainName 'evotec.pl' -DNSProvider Cloudflare | Format-Table
Find-AutoDiscoverRecord -DomainName 'evotec.pl' | Format-Table