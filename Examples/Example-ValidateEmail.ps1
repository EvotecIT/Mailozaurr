Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Write-Color -Text "Testing via parameter" -Color Green

Test-EmailAddress -EmailAddress 'evotec@test', 'evotec@test.pl', 'evotec.@test.pl', 'evotec.p@test.pl.', 'olly@somewhere...com', 'olly@somewhere.', 'olly@somewhere', 'user@☎.com', '.@domain.tld' | Format-Table

Write-Color -Text "Testing via pipeline" -Color Green

'evotec@test', 'evotec@test.pl', 'evotec.@test.pl', 'evotec.p@test.pl.', 'olly@somewhere...com', 'olly@somewhere.', 'olly@somewhere', 'user@☎.com', '.@domain.tld', "testme@zumpul.com" | Test-EmailAddress -Verbose | Format-Table