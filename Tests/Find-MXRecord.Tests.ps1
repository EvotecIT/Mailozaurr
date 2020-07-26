Describe 'Find-MxRecord' {
    It 'Given 2 domains it should return two records' {

        $DNS = Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].MX | Should -Be 'evotec-pl.mail.protection.outlook.com'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].MX | Should -Be 'evotec-xyz.mail.protection.outlook.com'
    }
}