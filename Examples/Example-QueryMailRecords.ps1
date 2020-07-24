Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *