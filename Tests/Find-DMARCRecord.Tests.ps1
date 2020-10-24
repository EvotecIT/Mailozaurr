Describe 'Find-DMARCRecord' {
    It 'Given 2 domains it should return two records' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1;p=none;rua=mailto:dmarc_agg@vali.email;sp=none;aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be ''
    }
    It 'Given 2 domains it should return two records using specific server' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1;p=none;rua=mailto:dmarc_agg@vali.email;sp=none;aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be ''
        $DNS[0].QueryServer | Should -Be '1.1.1.1:53'
        $DNS[1].QueryServer | Should -Be '1.1.1.1:53'
    }
    It 'Given 2 domains it should return two records using HTTPS GOOGLE' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1;p=none;rua=mailto:dmarc_agg@vali.email;sp=none;aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be ''
        $DNS[0].QueryServer | Should -Be 'dns.google.com'
        $DNS[1].QueryServer | Should -Be 'dns.google.com'
    }
    It 'Given 2 domains it should return two records using HTTPS Cloudflare' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1;p=none;rua=mailto:dmarc_agg@vali.email;sp=none;aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be ''
        $DNS[0].QueryServer | Should -Be 'cloudflare-dns.com'
        $DNS[1].QueryServer | Should -Be 'cloudflare-dns.com'
    }
}