Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -Selector 'selector1' | Format-Table *

# Or query specific server:

Find-MxRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1 | Format-Table *
Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1 | Format-Table *
Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1 | Format-Table *
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -DnsServer 1.1.1.1 | Format-Table *
Find-DKIMRecord -DomainName 'evotec.pl', 'evotec.xyz' -Selector 'selector1' -DnsServer 1.1.1.1 | Format-Table *

<#
Name       Count Preference TTL MX                                     IPAddress
----       ----- ---------- --- --                                     ---------
evotec.pl      1 10         1   evotec-pl.mail.protection.outlook.com  104.47.10.36; 104.47.8.36
evotec.xyz     1 0          60  evotec-xyz.mail.protection.outlook.com 104.47.8.36; 104.47.9.36
#>
<#
Name       TTL Count DMARC
----       --- ----- -----
evotec.pl  60      1 v=DMARC1;p=none;rua=mailto:dmarc_agg@vali.email;sp=none;aspf=s;
evotec.xyz         0
#>
<#
Name       TTL Count SPF
----       --- ----- ---
evotec.pl  11      1 v=spf1 a mx ip4:77.55.95.155 ip4:37.59.176.138 ip4:37.59.176.137 include:spf.protection.outlook.com -all
evotec.xyz 55      1 v=spf1 include:spf.protection.outlook.com -all
#>
<#
Name       Count Selector             DKIM
----       ----- --------             ----
evotec.pl      1 evotec.pl:selector1  v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCqrIpQkyykYEQbNzvHfgGsiYfoyX3b3Z6CPMHa5aNn/Bd8skLaqwK9vj2fHn70DA+X67L/pV2U5VYDzb5AUfQeD6NPDwZ7zLRc0XtX+5jyHWhHueSQT8uo6acMA+9JrVHdRfvtlQo8Oag8SLIkhaUea3xqZpijkQR/qHmo3GIfnQIDAQAB;
evotec.xyz     1 evotec.xyz:selector1 v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDhI50wSneU7XBAY5e/L0EYs7DtoEN2MV4S9nZt9LpSIliNvlOXM2hJ9N4ks1dga5HwnfLH8LCudOQD/m+Me8S8V+xQXEAd6uHFOKiZiup8VLor07eBs5A6OqmQ4hnJ0ioo54KHfhqRAHnC5e/oo26wuntHAzQsaTuQEIRPY8v1aQIDAQAB; n=1024,1449053169,1
#>