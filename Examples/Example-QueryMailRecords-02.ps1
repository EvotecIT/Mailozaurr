# Another way
Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Domains = @(
    @{ DomainName = 'evotec.pl'; Selector = 'selector1' }
    @{ DomainName = 'evotec.xyz'; Selector = 'selector1' }
    @{ DomainName = 'microsoft.com'; Selector = 'selector2' }
)

Find-MxRecord -DomainName $Domains | Format-Table *
Find-DMARCRecord -DomainName $Domains | Format-Table *
Find-SPFRecord -DomainName $Domains | Format-Table *
Find-DKIMRecord -DomainName $Domains | Format-Table *