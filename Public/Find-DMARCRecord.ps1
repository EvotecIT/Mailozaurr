function Find-DMARCRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline)][string[]] $DomainName,
        [System.Net.IPAddress] $DnsServer,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            $Splat = @{
                Name        = "_dmarc.$Domain"
                Type        = "txt"
                ErrorAction = "Stop"
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            try {
                $DNSRecord = Resolve-DnsName @Splat | Where-Object Strings -Match "DMARC1"
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name  = $Domain
                        TTL   = $DnsRecord.TTL -join ' ;'
                        Count = $DNSRecord.Count
                        DMARC = $DnsRecord.Strings -join ' ;'
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name  = $Domain
                        TTL   = $DnsRecord.TTL
                        Count = $DNSRecord.Count
                        DMARC = $DnsRecord.Strings
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name  = $Domain
                    TTL   = ''
                    Count = 0
                    DMARC = ''
                }
                Write-Warning "Find-DMARCRecord - $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}