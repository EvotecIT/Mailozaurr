function Find-SPFRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][string[]]$DomainName,
        [System.Net.IPAddress] $DnsServer,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            $Splat = @{
                Name        = $Domain
                Type        = "txt"
                ErrorAction = "Stop"
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            try {
                $DNSRecord = Resolve-DnsName @Splat | Where-Object Strings -Match "spf1"
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name  = $Domain
                        TTL   = $DnsRecord.TTL -join ' ;'
                        Count = $DNSRecord.Count
                        SPF   = $DnsRecord.Strings -join ' ;'
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name  = $Domain
                        TTL   = $DnsRecord.TTL
                        Count = $DNSRecord.Count
                        SPF   = $DnsRecord.Strings
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name  = $Domain
                    TTL   = ''
                    Count = 0
                    SPF   = ''
                }
                Write-Warning "Find-SPFRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}