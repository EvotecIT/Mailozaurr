function Find-DKIMRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][string[]] $DomainName,
        [string[]] $Selector = "selector1",
        [System.Net.IPAddress] $DnsServer,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            foreach ($S in $Selector) {
                $Splat = @{
                    Name        = "$S._domainkey.$Domain"
                    Type        = "TXT"
                    ErrorAction = "SilentlyContinue"
                }
                if ($DnsServer) {
                    $Splat['Server'] = $DnsServer
                }
                $DNSRecord = Resolve-DnsName @Splat | Where-Object Strings -Match "DKIM1"
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name     = $Domain
                        Count    = $DNSRecord.Strings.Count
                        Selector = "$Domain`:$S"
                        DKIM     = $DNSRecord.Strings -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name     = $Domain
                        Count    = $DNSRecord.Strings.Count
                        Selector = "$Domain`:$S"
                        DKIM     = $DNSRecord.Strings -join '; '
                    }
                }
                if ($AsHashTable) {
                    $MailRecord
                } else {
                    [PSCustomObject] $MailRecord
                }
            }
        }
    }
}