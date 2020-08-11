function Find-SPFRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array]$DomainName,
        [string] $DnsServer,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $D = $Domain.DomainName
                if (-not $D) {
                    Write-Warning 'Find-MxRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = $D
                Type        = 'txt'
                ErrorAction = 'Stop'
            }
            if ($DnsServer) {
                $Splat['Server'] = $DnsServer
            }
            try {
                $DNSRecord = Resolve-DnsQuery @Splat -All
                $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'spf1'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '
                        SPF         = $DNSRecordAnswers.Text -join '; '
                        QueryServer = $DNSRecord.NameServer
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        SPF         = $DNSRecordAnswers.Text
                        QueryServer = $DNSRecord.NameServer
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name        = $D
                    Count       = 0
                    TimeToLive  = ''
                    SPF         = ''
                    QueryServer = ''
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