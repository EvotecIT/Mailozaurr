Describe 'Find-DMARCRecord' {
    It 'Given 2 domains it should return two records' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:1012c7e7df7b474cb85c1c8d00cc1c1a@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl; adkim=s; aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:fd0adf705bb54ff5919bab3a6c177ba0@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl'
    }
    It 'Given 2 domains it should return two records using specific server' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:1012c7e7df7b474cb85c1c8d00cc1c1a@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl; adkim=s; aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:fd0adf705bb54ff5919bab3a6c177ba0@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl'
        $DNS[0].QueryServer | Should -Be '1.1.1.1:53'
        $DNS[1].QueryServer | Should -Be '1.1.1.1:53'
    }
    It 'Given 2 domains it should return two records using HTTPS GOOGLE' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:1012c7e7df7b474cb85c1c8d00cc1c1a@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl; adkim=s; aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:fd0adf705bb54ff5919bab3a6c177ba0@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl'
        $DNS[0].QueryServer | Should -Be 'dns.google.com'
        $DNS[1].QueryServer | Should -Be 'dns.google.com'
    }
    It 'Given 2 domains it should return two records using HTTPS Cloudflare' {
        $DNS = Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:1012c7e7df7b474cb85c1c8d00cc1c1a@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl; adkim=s; aspf=s;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DMARC | Should -Be 'v=DMARC1; p=reject; rua=mailto:fd0adf705bb54ff5919bab3a6c177ba0@dmarc-reports.cloudflare.net,mailto:7kkoc19n@ag.eu.dmarcian.com,mailto:dmarc@evotec.pl'
        $DNS[0].QueryServer | Should -Be 'cloudflare-dns.com'
        $DNS[1].QueryServer | Should -Be 'cloudflare-dns.com'
    }
}