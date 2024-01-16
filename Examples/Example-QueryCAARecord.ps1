Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Find-CAARecord -DomainName "evotec.pl" -DNSProvider Cloudflare | Format-Table -AutoSize
Find-CAARecord -DomainName 'evotec.pl' -DNSProvider Google | Format-Table -AutoSize
Find-CAARecord -DomainName 'evotec.pl' -Verbose | Format-Table -AutoSize

Find-CAARecord -DomainName "mbank.pl" -DNSProvider Cloudflare | Format-Table -AutoSize
Find-CAARecord -DomainName 'mbank.pl' -DNSProvider Google | Format-Table -AutoSize
Find-CAARecord -DomainName 'mbank.pl' -Verbose | Format-Table -AutoSize