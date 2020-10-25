Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

Resolve-DnsQuery '96.48.74.89.zen.spamhaus.org' -Type A
Resolve-DnsQueryRest '96.48.74.89.zen.spamhaus.org' -Type A