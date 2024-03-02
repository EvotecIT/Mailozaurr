function Find-SPFRecord {
    <#
    .SYNOPSIS
    Queries DNS to provide SPF information

    .DESCRIPTION
    Queries DNS to provide SPF information

    .PARAMETER DomainName
    Name/DomainName to query for SPF record

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
    Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' | Format-Table *

    .EXAMPLE
    # Https way via Cloudflare
    Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Cloudflare | Format-Table *

    .EXAMPLE
    # Https way via Google
    Find-SPFRecord -DomainName 'evotec.pl', 'evotec.xyz' -DNSProvider Google | Format-Table *

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array]$DomainName,
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $AsHashTable,
        [switch] $AsObject
    )
    begin {
        $SPFTags = [ordered] @{
            'v'        = 'SPFVersion'
            'all'      = 'All'
            #'a'        = 'A'
            #'mx'       = 'MX'
            #'ptr'      = 'PTR'
            #'ip4'      = 'IP4'
            #'ip6'      = 'IP6'
            #'include'  = 'Include'
            #'exists'   = 'Exists'
            'redirect' = 'Redirect'
            'exp'      = 'Exp'
        }
    }
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
            try {
                if ($DNSProvider) {
                    $DNSRecord = Resolve-DnsQueryRest @Splat -All -DNSProvider $DnsProvider
                } else {
                    if ($DnsServer) {
                        $Splat['Server'] = $DnsServer
                    }
                    $DNSRecord = Resolve-DnsQuery @Splat -All
                }
                $DNSRecordAnswers = $DNSRecord.Answers | Where-Object Text -Match 'spf1'
                if ($DNSRecordAnswers -is [array]) {
                    $Count = $DNSRecordAnswers.Count
                } elseif ($DNSRecordAnswers) {
                    $Count = 1
                } else {
                    $Count = 0
                }
                if (-not $AsObject) {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        #Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive -join '; '
                        SPF         = $DNSRecordAnswers.Text -join '; '
                        QueryServer = $DNSRecord.NameServer
                        Advisory    = "No SPF record found."
                    }
                } else {
                    $MailRecord = [ordered] @{
                        Name        = $D
                        #Count       = $DNSRecordAnswers.Count
                        TimeToLive  = $DNSRecordAnswers.TimeToLive
                        SPF         = $DNSRecordAnswers.Text
                        QueryServer = $DNSRecord.NameServer
                        Advisory    = "No SPF record found."
                    }
                }
                # Split the SPF record into an array of settings
                $SPFSettings = $MailRecord.SPF -split ' '
                # Initialize an empty hashtable to store the settings
                $SPFSettingsHashTable = [ordered] @{}
                # Loop through each setting
                foreach ($setting in $SPFSettings) {
                    # Check if the setting is the SPF version
                    if ($setting -match '^v=') {
                        $Key = 'v'
                        $Value = $setting -replace 'v=', ''
                    } elseif ($setting -match '^[+~-]?all$') {
                        # Check if the setting is the 'all' mechanism
                        $Key = 'all'
                        switch ($setting) {
                            '+all' { $Value = 'Pass' }
                            '-all' { $Value = 'Fail' }
                            '~all' { $Value = 'SoftFail' }
                            '?all' { $Value = 'Neutral' }
                            default { $Value = 'Neutral' }
                        }
                    } elseif ($setting -match ':') {
                        # Check if the setting contains a colon
                        # Split the setting into a key-value pair
                        $KeyValuePair = $setting -split ':'
                        $Key = $KeyValuePair[0].Trim()
                        $Value = $KeyValuePair[1]
                    } else {
                        # If the setting does not contain a colon, the whole setting is the key
                        $Key = $setting.Trim()
                        $Value = $true
                    }
                    # Add the key-value pair to the hashtable
                    $SPFSettingsHashTable[$Key] = $Value
                }
                foreach ($Tag in $SPFTags.Keys) {
                    # If the tag is in the SPF record, add its value to the MailRecord
                    if ($SPFSettingsHashTable[$Tag]) {
                        $MailRecord[$SPFTags[$Tag]] = $SPFSettingsHashTable[$Tag]
                    } else {
                        $MailRecord[$SPFTags[$tag]] = $null
                    }
                }
                # Lets do some advisory logic
                if ($Count -eq 0) {
                    $MailRecord['Advisory'] = "No SPF record found."
                } elseif ($Count -gt 1) {
                    $MailRecord['Advisory'] = "Multiple SPF records found. This is not allowed (as per RFC4408)"
                } else {
                    switch -Regex ($MailRecord.SPF) {
                        '-all' {
                            $SpfAdvisory = "An SPF record is configured and the policy is strict."
                        }
                        '~all' {
                            $SpfAdvisory = "An SPF record is configured but the policy is not strict."
                        }
                        "\?all" {
                            $SpfAdvisory = "An SPF record is configured but the policy is not effective."
                        }
                        '\+all' {
                            $SpfAdvisory = "An SPF record is configured but the policy is not effective."
                        }
                        Default {
                            $SpfAdvisory = "No qualifier found. Policy is not effective."
                        }
                    }
                    if ($MailRecord.SPF.Length -gt 255) {
                        $SpfAdvisory = "SPF record is too long. It's recommended to keep it under 255 characters (as per RFC4408)"
                    } else {
                        $MailRecord['Advisory'] = $SpfAdvisory
                    }
                }

                # Check if there are more than 10 DNS lookups in the SPF record
                $dnsLookupMechanisms = 'a', 'mx', 'ptr', 'exists', 'include', 'redirect'
                $dnsLookupCount = ($SPFSettings | Where-Object { $_ -in $dnsLookupMechanisms }).Count
                if ($dnsLookupCount -gt 10) {
                    $SpfAdvisory = "Invalid SPF record - SPF record requires more than 10 DNS lookups"
                }
                # Split each setting into its mechanism/modifier name and value
                $SPFSettingNames = $SPFSettings | ForEach-Object {
                    if ($_ -match '[:=]') {
                        $settingName = $_ -split '[:=]' | Select-Object -First 1
                    } else {
                        $settingName = $_
                    }
                    return $settingName
                }
                # Check if there are any unrecognized mechanisms or modifiers
                $validMechanismsAndModifiers = 'v', 'a', 'mx', 'ptr', 'ip4', 'ip6', 'include', 'exists', 'redirect', 'exp', '-all', '~all', '?all', '+all'
                $invalidMechanismsAndModifiers = $SPFSettingNames | Where-Object { $_ -notin $validMechanismsAndModifiers }
                if ($invalidMechanismsAndModifiers) {
                    $SpfAdvisory = "Invalid SPF record - SPF record contains unrecognized mechanisms or modifiers: $($invalidMechanismsAndModifiers -join ', ')"
                }
                # Check if the SPF record starts with 'v=spf1'
                if ($SPFSettings[0] -ne 'v=spf1') {
                    $SpfAdvisory = "Invalid SPF record - SPF record does not start with 'v=spf1'"
                }
                # Check if there is at least one mechanism present
                $mechanisms = 'all', 'a', 'mx', 'ptr', 'ip4', 'ip6', 'include', 'exists', 'redirect', 'exp'
                if (-not ($SPFSettingsHashTable.Keys | Where-Object { $_ -in $mechanisms })) {
                    $SpfAdvisory = "Invalid SPF record - no mechanisms present"
                }
                # Check if the 'all' mechanism, if present, is the last mechanism in the record
                $allMechanism = $SPFSettings | Where-Object { $_ -match '^[+~-]?all$' }
                if ($allMechanism -and $SPFSettings[-1] -notmatch '^[+~-]?all$') {
                    $SpfAdvisory = "Invalid SPF record - 'all' mechanism is not the last mechanism"
                }
                $MailRecord['Advisory'] = $SpfAdvisory
            } catch {
                $MailRecord = [ordered] @{
                    Name        = $D
                    #Count       = 0
                    TimeToLive  = ''
                    SPF         = ''
                    QueryServer = ''
                    Advisory    = "No SPF record found."
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