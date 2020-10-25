Describe 'Find-DNSBL' {
    It 'Given 1 ip it should return just 5 entries' {
        $DNS = Find-DNSBL -IP '89.74.48.96'
        $DNS.Count | Should -BeGreaterThan 2
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
    }
    It 'Given 1 ip it should return 71 with All switch' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -All
        $DNS.Count | Should -Be 71
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
    }
    It 'Given 1 ip it should return just 5 entries using 1.1.1.1 dns' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -DNSServer 1.1.1.1
        $DNS.Count | Should -BeGreaterThan 2
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = '1.1.1.1:53'
    }
    It 'Given 1 ip it should return 71 with All switch using 1.1.1.1 dns' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -All -DNSServer 1.1.1.1
        $DNS.Count | Should -Be 71
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = '1.1.1.1:53'
    }
    It 'Given 1 ip it should return just 5 entries using HTTPS Cloudflare' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -DNSProvider Cloudflare
        $DNS.Count | Should -BeGreaterThan 2
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = 'cloudflare-dns.com'
    }
    It 'Given 1 ip it should return 71 with All switch using HTTPS Cloudflare' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -All -DNSProvider Cloudflare
        $DNS.Count | Should -Be 71
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = 'cloudflare-dns.com'
    }
    It 'Given 1 ip it should return just 5 entries using HTTPS Google' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -DNSProvider Google
        $DNS.Count | Should -BeGreaterThan 2
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = 'dns.google.com'
    }
    It 'Given 1 ip it should return 71 with All switch HTTPS Google' {
        $DNS = Find-DNSBL -IP '89.74.48.96' -All -DNSProvider Google
        $DNS.Count | Should -Be 71
        $DNS[0].IP = '89.74.48.96'
        $DNS[0].FQDN = '96.48.74.89.b.barracudacentral.org'
        $DNS[0].BlackList = 'b.barracudacentral.org'
        $DNS[0].NameServer = 'dns.google.com'
    }
}