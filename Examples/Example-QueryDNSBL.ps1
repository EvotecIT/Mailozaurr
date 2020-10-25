Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Find-DNSBL -IP '89.74.48.96' | Format-Table

Find-DNSBL -IP '89.74.48.96', '89.74.48.97', '89.74.48.98' | Format-Table

Find-DNSBL -IP '89.74.48.96' -DNSServer 1.1.1.1 | Format-Table

Find-DNSBL -IP '89.74.48.96' -DNSProvider Google | Format-Table

Find-DNSBL -IP '89.74.48.96' -DNSProvider Cloudflare | Format-Table