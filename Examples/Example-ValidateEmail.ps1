Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Test-EmailAddress -EmailAddress 'evotec@test', 'evotec@test.pl', 'evotec.@test.pl', 'evotec.p@test.pl.', 'olly@somewhere...com', 'olly@somewhere.', 'olly@somewhere', 'user@☎.com', '.@domain.tld'