Describe 'Find-DKIMRecord' {
    It 'Given 2 domains it should return two records' {
        $DNS = Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz'
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhI50wSneU7XBAY5e/L0EYs7DtoEN2MV4S9nZt9LpSIliNvlOXM2hJ9N4ks1dga5HwnfLH8LCudOQD/m+Me8S8V+xQXEAd6uHFOKiZiup8VLor07eBs5A6OqmQ4hnJ0ioo54KHfhqRAHnC5e/oo26wuntHAzQsaTuQEIRPY8v1aQIDAQAB; n=1024,1449053169,1'
        $DNS[0].Selector | Should -Be 'evotec.pl:selector1'
        $DNS[1].Selector | Should -Be 'evotec.xyz:selector1'
    }
    It 'Given 2 domains it should return two records using specific server' {
        $DNS = Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhI50wSneU7XBAY5e/L0EYs7DtoEN2MV4S9nZt9LpSIliNvlOXM2hJ9N4ks1dga5HwnfLH8LCudOQD/m+Me8S8V+xQXEAd6uHFOKiZiup8VLor07eBs5A6OqmQ4hnJ0ioo54KHfhqRAHnC5e/oo26wuntHAzQsaTuQEIRPY8v1aQIDAQAB; n=1024,1449053169,1'
        $DNS[0].QueryServer | Should -Be '1.1.1.1:53'
        $DNS[1].QueryServer | Should -Be '1.1.1.1:53'
        $DNS[0].Selector | Should -Be 'evotec.pl:selector1'
        $DNS[1].Selector | Should -Be 'evotec.xyz:selector1'
    }
    It 'Given 2 domains it should return two records using HTTPS GOOGLE' {
        $DNS = Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhI50wSneU7XBAY5e/L0EYs7DtoEN2MV4S9nZt9LpSIliNvlOXM2hJ9N4ks1dga5HwnfLH8LCudOQD/m+Me8S8V+xQXEAd6uHFOKiZiup8VLor07eBs5A6OqmQ4hnJ0ioo54KHfhqRAHnC5e/oo26wuntHAzQsaTuQEIRPY8v1aQIDAQAB; n=1024,1449053169,1'
        $DNS[0].QueryServer | Should -Be 'dns.google.com'
        $DNS[1].QueryServer | Should -Be 'dns.google.com'
        $DNS[0].Selector | Should -Be 'evotec.pl:selector1'
        $DNS[1].Selector | Should -Be 'evotec.xyz:selector1'
    }
    It 'Given 2 domains it should return two records using HTTPS Cloudflare' {
        $DNS = Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare
        $DNS.Count | Should -Be 2
        $DNS[0].Name | Should -Be 'evotec.pl'
        $DNS[0].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;'
        $DNS[1].Name | Should -Be 'evotec.xyz'
        $DNS[1].DKIM | Should -Be 'v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhI50wSneU7XBAY5e/L0EYs7DtoEN2MV4S9nZt9LpSIliNvlOXM2hJ9N4ks1dga5HwnfLH8LCudOQD/m+Me8S8V+xQXEAd6uHFOKiZiup8VLor07eBs5A6OqmQ4hnJ0ioo54KHfhqRAHnC5e/oo26wuntHAzQsaTuQEIRPY8v1aQIDAQAB; n=1024,1449053169,1'
        $DNS[0].QueryServer | Should -Be 'cloudflare-dns.com'
        $DNS[1].QueryServer | Should -Be 'cloudflare-dns.com'
        $DNS[0].Selector | Should -Be 'evotec.pl:selector1'
        $DNS[1].Selector | Should -Be 'evotec.xyz:selector1'
    }
}