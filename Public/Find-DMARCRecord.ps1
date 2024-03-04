function Find-DMARCRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide DMARC information

    .DESCRIPTION
    Queries DNS to provide DMARC information

    .PARAMETER DomainName
    Name/DomainName to query for DMARC record

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .EXAMPLE
    # Standard way
    Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

    .EXAMPLE
    # Https way via Cloudflare
    Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

    .EXAMPLE
    # Https way via Google
    Find-DMARCRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    begin {
        # $DMARCTagsNames = [ordered] @{
        #     'v'     = 'Protocol version'
        #     'p'     = 'Policy for the domain'
        #     'sp'    = 'Policy for subdomains'
        #     'rua'   = 'Reporting URI of aggregate reports'
        #     'ruf'   = 'Reporting URI of forensic reports'
        #     'pct'   = 'Percentage of messages subjected to filtering'
        #     'adkim' = 'Alignment mode for DKIM'
        #     'aspf'  = 'Alignment mode for SPF'
        #     'ri'    = 'Reporting interval'
        #     'fo'    = 'Failure reporting options'
        # }
        $DMARCTags = [ordered] @{
            'v'     = 'ProtocolVersion'
            'p'     = 'Policy'
            'sp'    = 'SubdomainPolicy'
            'rua'   = 'AggregateReportURI'
            'ruf'   = 'ForensicReportURI'
            'pct'   = 'Percent' # This Value is the percentage of email to be surveyed and reported back to the domain owner.
            'adkim' = 'DKIMAlignmentMode'
            'aspf'  = 'SPFAlignmentMode'
            'ri'    = 'ReportInterval'
            'fo'    = 'FailureOptions'
        }
    }
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $D = $Domain.DomainName
                if (-not $D) {
                    Write-Warning 'Find-DMARCRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "_dmarc.$D"
                Type        = 'TXT'
                ErrorAction = 'Stop'
            }
            try {
                if ($DNSProvider) {
                    $DNSRecord = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
                } else {
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    $DNSRecord = Resolve-DnsQuery @Splat -All
                }
                $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'DMARC1'
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        #Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '
                        DMARC       = $DNSRecordAnswers.Text -join '; '
                        QueryServer = $DNSRecord.NameServer -join '; '
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        #Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        DMARC       = $DNSRecordAnswers.Text
                        QueryServer = $DNSRecord.NameServer
                    }
                }
                $MailRecord['PolicyAdvisory'] = "Domain has NO DMARC record. This domain is at risk to being abused."
                $MailRecord['SubdomainPolicyAdvisory'] = $null

                # Split the DMARC record into an array of settings
                $DMARCSettings = $MailRecord.DMARC -split ';'

                # # Initialize an empty hashtable to store the settings
                $DMARCSettingsHashTable = [ordered] @{}

                # Loop through each setting
                foreach ($setting in $DMARCSettings) {
                    # Split the setting into a key-value pair
                    $KeyValuePair = $setting -split '='
                    $Key = $KeyValuePair[0].Trim()
                    $Value = $KeyValuePair[1]
                    # Translate 's' to 'Strict mode' and 'r' to 'Relaxed mode' for 'adkim' and 'aspf'
                    if ($Key -eq 'adkim' -or $Key -eq 'aspf') {
                        switch ($Value) {
                            's' { $Value = 'Strict mode' }
                            'r' { $Value = 'Relaxed mode' }
                        }
                    } elseif ($Key -eq 'fo') {
                        # Translate values for 'fo'
                        $foValues = $Value -split ':'
                        $translatedFoValues = @()
                        foreach ($foValue in $foValues) {
                            switch ($foValue) {
                                '0' { $translatedFoValues += 'Generate report if all mechanisms fail (0)' }
                                '1' { $translatedFoValues += 'Generate report if any mechanism fails (1)' }
                                'd' { $translatedFoValues += 'Generate report if DKIM test fails (d)' }
                                's' { $translatedFoValues += 'Generate report if SPF test fails (s)' }
                            }
                        }
                        $Value = $translatedFoValues -join ', '
                    } elseif ($Key -eq 'ri') {
                        $ValueInSeconds = [int]$Value
                        if ($ValueInSeconds -ge 86400 -and $ValueInSeconds % 86400 -eq 0) {
                            $ValueInDays = $ValueInSeconds / 86400
                            $Value = "$ValueInDays day(s)"
                        } elseif ($ValueInSeconds -ge 3600 -and $ValueInSeconds % 3600 -eq 0) {
                            $ValueInHours = $ValueInSeconds / 3600
                            $Value = "$ValueInHours hour(s)"
                        } else {
                            $Value = "$ValueInSeconds second(s)"
                        }
                    }
                    # Add the key-value pair to the hashtable
                    $DMARCSettingsHashTable[$Key] = $Value
                }
                # Check if 'adkim' and 'aspf' are defined in the DMARC record
                if (-not $DMARCSettingsHashTable['adkim']) {
                    # If 'adkim' is not defined, set it to 'Relaxed (default)'
                    $DMARCSettingsHashTable['adkim'] = 'Relaxed (default)'
                }
                if (-not $DMARCSettingsHashTable['aspf']) {
                    # If 'aspf' is not defined, set it to 'Relaxed (default)'
                    $DMARCSettingsHashTable['aspf'] = 'Relaxed (default)'
                }
                # Check if 'pct' is defined in the DMARC record
                if (-not $DMARCSettingsHashTable['pct']) {
                    # If 'pct' is not defined, set it to 100
                    # as per the DMARC specification
                    $DMARCSettingsHashTable['pct'] = '100'
                }
                # Check if 'fo' is defined in the DMARC record
                if ($DMARCSettingsHashTable.Keys -notcontains 'fo') {
                    # If 'fo' is not defined, set it to 'Generate report if all mechanisms fail (default)'
                    $DMARCSettingsHashTable['fo'] = 'Generate report if all mechanisms fail (default - 0)'
                }
                # Check if 'ri' is defined in the DMARC record
                if ($DMARCSettingsHashTable.Keys -notcontains 'ri') {
                    # If 'ri' is not defined, set it to '1 day (default)'
                    $DMARCSettingsHashTable['ri'] = '1 day (default)'
                }
                foreach ($Tag in $DMARCTags.Keys) {
                    # If the tag is in the DMARC record, add its value to the MailRecord
                    if ($DMARCSettingsHashTable[$Tag]) {
                        if ($Tag -eq 'rua' -or $Tag -eq 'ruf') {
                            if ($Tag -eq 'rua') {
                                $MailRecord['AggregateReportEmail'] = $null
                                $MailRecord['AggregateReportHTTP'] = $null
                            } else {
                                $MailRecord['ForensicReportEmail'] = $null
                                $MailRecord['ForensicReportHTTP'] = $null
                            }
                            if ($DMARCSettingsHashTable[$Tag] -match '^mailto:') {
                                # Check if the value starts with 'mailto:'
                                $Key = if ($Tag -eq 'rua') { 'AggregateReportEmail' } else { 'ForensicReportEmail' }
                                $MailRecord[$Key] = $DMARCSettingsHashTable[$Tag].Replace('mailto:', '') -split ','
                            } elseif ($DMARCSettingsHashTable[$Tag] -match '^http:') {
                                # Check if the value starts with 'http:'
                                $Key = if ($Tag -eq 'rua') { 'AggregateReportHTTP' } else { 'ForensicReportHTTP' }
                                $MailRecord[$Key] = $DMARCSettingsHashTable[$Tag] -split ','
                            }
                        } else {
                            $MailRecord[$DMARCTags[$Tag]] = $DMARCSettingsHashTable[$Tag]
                        }
                    } else {
                        $MailRecord[$DMARCTags[$tag]] = $null
                    }
                }
                switch ($MailRecord.Policy) {
                    'none' {
                        $MailRecord['PolicyAdvisory'] = "Domain has a DMARC policy (p=none), but the DMARC policy does not prevent abuse."
                    }
                    'quarantine' {
                        $MailRecord['PolicyAdvisory'] = "Domain has a DMARC policy (p=quarantine), and will prevent abuse of domain, but finally should be set to p=reject."
                    }
                    'reject' {
                        $MailRecord['PolicyAdvisory'] = "Domain has a DMARC policy (p=reject), and will prevent abuse of domain."
                    }
                }
                switch ($MailRecord.SubdomainPolicy) {
                    'none' {
                        $MailRecord['SubdomainPolicyAdvisory'] = "Subdomain has a DMARC policy (sp=none), but the DMARC policy does not prevent abuse."
                    }
                    'quarantine' {
                        $MailRecord['SubdomainPolicyAdvisory'] = "Subdomain has a DMARC policy (sp=quarantine), and will prevent abuse of domain, but finally should be set to sp=reject."
                    }
                    'reject' {
                        $MailRecord['SubdomainPolicyAdvisory'] = "Subdomain has a DMARC policy (sp=reject), and will prevent abuse of your domain."
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name        = $D
                    #Count       = 0
                    TimeToLive  = ''
                    DMARC       = ''
                    QueryServer = ''
                    Advisory    = "No DMARC record found."
                }
                foreach ($Tag in $DMARCTags.Keys) {
                    if ($Tag -eq 'rua' -or $Tag -eq 'ruf') {
                        if ($Tag -eq 'rua') {
                            $MailRecord['AggregateReportEmail'] = $null
                            $MailRecord['AggregateReportHTTP'] = $null
                        } else {
                            $MailRecord['ForensicReportEmail'] = $null
                            $MailRecord['ForensicReportHTTP'] = $null
                        }
                    } else {
                        $MailRecord[$DMARCTags[$Tag]] = $null
                    }
                }
                Write-Warning "Find-DMARCRecord - Error $_"
            }
            if ($AsHashTable) {
                $MailRecord
            } else {
                [PSCustomObject] $MailRecord
            }
        }
    }
}