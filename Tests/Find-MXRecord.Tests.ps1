Describe 'Find-MxRecord' {
    It 'Given 2 domains it should return two records' {
        $DNS = Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].MX | Should -Be 'evotec-pl.mail.protection.outlook.com'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].MX | Should -Be 'evotec-xyz.mail.protection.outlook.com'
    }
    It 'Given 2 domains it should return two records using specific server' {
        $DNS = Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].MX | Should -Be 'evotec-pl.mail.protection.outlook.com'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].MX | Should -Be 'evotec-xyz.mail.protection.outlook.com'
    }
    It 'Given 2 domains it should return two records using HTTPS GOOGLE' {
        $DNS = Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].MX | Should -Be 'evotec-pl.mail.protection.outlook.com'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].MX | Should -Be 'evotec-xyz.mail.protection.outlook.com'
    }
    It 'Given 2 domains it should return two records using HTTPS Cloudflare' {
        $DNS = Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].MX | Should -Be 'evotec-pl.mail.protection.outlook.com'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].MX | Should -Be 'evotec-xyz.mail.protection.outlook.com'
    }
}