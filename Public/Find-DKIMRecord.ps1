function Find-DKIMRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $Selector = 'selector1',
        [string] $DnsServer,
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
            $DNSRecord = Resolve-DnsQuery @Splat -All
            $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'DKIM1'
            if (-not $AsObject) {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = $DNSRecordAnswers.Text.Count
                    Selector    = "$D`:$S"
                    DKIM        = $DNSRecordAnswers.Text -join '; '
                    QueryServer = $DNSRecord.NameServer
                }
            } else {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = $DNSRecordAnswers.Text.Count
                    Selector    = "$D`:$S"
                    DKIM        = $DNSRecordAnswers.Text -join '; '
                    QueryServer = $DNSRecord.NameServer -join '; '
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