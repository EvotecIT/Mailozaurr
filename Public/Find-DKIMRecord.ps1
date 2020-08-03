function Find-DKIMRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $Selector = 'selector1',
        [System.Net.IPAddress] $DnsServer,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $S = $Selector
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $S = $Domain.Selector
                $D = $Domain.DomainName
                if (-not $S -and -not $D) {
                    Write-Warning 'Find-DKIMRecord - properties DomainName and Selector are required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "$S._domainkey.$D"
                Type        = 'TXT'
                ErrorAction = 'SilentlyContinue'
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            $DNSRecord = Resolve-DnsQuery @Splat | Where-Object Text -Match 'DKIM1'
            if (-not $AsObject) {
                $MailRecord = [ordered] @{
                    Name     = $D
                    Count    = $DNSRecord.Text.Count
                    Selector = "$D`:$S"
                    DKIM     = $DNSRecord.Text -join '; '
                }
            } else {
                $MailRecord = [ordered] @{
                    Name     = $D
                    Count    = $DNSRecord.Text.Count
                    Selector = "$D`:$S"
                    DKIM     = $DNSRecord.Text -join '; '
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