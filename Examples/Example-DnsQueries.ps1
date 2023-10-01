Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Resolve-DnsQuery '96.48.74.89.zen.spamhaus.org' -Type A
Resolve-DnsQueryRest '96.48.74.89.zen.spamhaus.org' -Type A

$Output1 = Resolve-DnsQuery -Name 'evotec.pl' -Type NS -All
$Output1 | Format-Table

$Output2 = Resolve-DnsQueryRest -Name 'evotec.pl' -Type NS -All -DNSProvider Google
$Output2 | Format-Table

$Output3 = Resolve-DnsQueryRest -Name 'evotec.pl' -Type NS -All -DNSProvider Cloudflare
$Output3 | Format-Table

$Mx = Resolve-DnsQuery -Name 'evotec.pl' -Type MX
$Mx

$Mx = Resolve-DnsQueryRest -Name 'google.com' -Type MX
$Mx