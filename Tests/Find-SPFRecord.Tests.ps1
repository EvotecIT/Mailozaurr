Describe 'Find-SPFRecord' {
    It 'Given 2 domains it should return two records' {
        $DNS = Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].SPF | Should -Be 'v=spf1 a mx ip4:77.55.95.155 ip4:37.59.176.138 ip4:37.59.176.137 include:spf.protection.outlook.com -all'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].SPF | Should -Be 'v=spf1 include:spf.protection.outlook.com -all'
    }
    It 'Given 2 domains it should return two records using specific server' {
        $DNS = Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].SPF | Should -Be 'v=spf1 a mx ip4:77.55.95.155 ip4:37.59.176.138 ip4:37.59.176.137 include:spf.protection.outlook.com -all'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].SPF | Should -Be 'v=spf1 include:spf.protection.outlook.com -all'
        $DNS[0].QueryServer | Should -Be '1.1.1.1:53'
        $DNS[1].QueryServer | Should -Be '1.1.1.1:53'
    }
    It 'Given 2 domains it should return two records using HTTPS GOOGLE' {
        $DNS = Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].SPF | Should -Be 'v=spf1 a mx ip4:77.55.95.155 ip4:37.59.176.138 ip4:37.59.176.137 include:spf.protection.outlook.com -all'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].SPF | Should -Be 'v=spf1 include:spf.protection.outlook.com -all'
        $DNS[0].QueryServer | Should -Be 'dns.google.com'
        $DNS[1].QueryServer | Should -Be 'dns.google.com'
    }
    It 'Given 2 domains it should return two records using HTTPS Cloudflare' {
        $DNS = Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].SPF | Should -Be 'v=spf1 a mx ip4:77.55.95.155 ip4:37.59.176.138 ip4:37.59.176.137 include:spf.protection.outlook.com -all'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].SPF | Should -Be 'v=spf1 include:spf.protection.outlook.com -all'
        $DNS[0].QueryServer | Should -Be 'cloudflare-dns.com'
        $DNS[1].QueryServer | Should -Be 'cloudflare-dns.com'
    }
}