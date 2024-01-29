function Find-IPGeolocation {
    <#
    .SYNOPSIS
    Get IP Geolocation information from ip-api.com

    .DESCRIPTION
    Get IP Geolocation information from ip-api.com
    It uses ip-api.com free API to get geolocation information.
    This API is limited to 45 requests per minute from an IP address.

    .PARAMETER IPAddress
    Parameter description

    .EXAMPLE
    Find-IPGeolocation -IPAddress '1.1.1.1'

    .EXAMPLE
    Find-IPGeolocation -IPAddress '1.1.1.1', '1.1.1.2'

    .NOTES
    Due to free API usage it's not possible to query API using HTTPS.

    #>
    [alias('Get-IPGeolocation')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string[]] $IPAddress,
        [ValidateSet(
            'status', 'message', 'continent', 'continentCode', 'country', 'countryCode', 'region', 'regionName', 'city', 'district',
            'zip', 'lat', 'lon', 'timezone', 'offset', 'currency', 'isp', 'org', 'as', 'asname', 'reverse', 'mobile', 'proxy', 'hosting', 'query'
        )][string[]] $Fields,
        [switch] $Force
    )

    if (-not $Script:CachedGEO -or $Force.IsPresent) {
        $Script:CachedGEO = [ordered] @{}
    }
    foreach ($IP in $IPAddress) {
        $QueryParameters = [ordered] @{
            fields = $Fields
        }
        Remove-EmptyValue -Hashtable $QueryParameters
        $Uri = Join-UriQuery -BaseUri 'http://ip-api.com/json' -QueryParameter $QueryParameters -RelativeOrAbsoluteUri $IP

        #$Result = Invoke-RestMethod -Uri $Uri -Method Get -ErrorAction Stop
        if (-not $Script:CachedGEO[$IP]) {
            Write-Verbose -Message "Find-IPGeolocation - Querying API for $IP"
            if ($Script:GeoHeaders -and $Script:GeoHeaders.Date -and $Script:GeoHeaders.Date.AddSeconds(60) -gt [DateTime]::Now) {
                if ($Script:GeoHeaders.Headers.'X-Rl' -le 0) {
                    $TimeToSleep = $Script:GeoHeaders.Headers.'X-Ttl' + 1
                    Write-Warning -Message "Get-IPGeolocation - API limit reached. Waiting $TimeToSleep seconds."
                    Start-Sleep -Seconds $TimeToSleep
                }
            }
            try {
                $Result = Invoke-WebRequest -Uri $Uri -Method Get -ErrorAction Stop -UseBasicParsing -Verbose:$false
                $Script:GeoHeaders = [ordered] @{
                    Date    = [DateTime]::Now
                    Headers = $Result.Headers
                }
                $ResultJSON = $Result.Content | ConvertFrom-Json -ErrorAction Stop
                if ($ResultJSON) {
                    $Script:CachedGEO[$IP] = $ResultJSON
                    $ResultJSON
                }
            } catch {
                Write-Warning -Message "Find-IPGeolocation - Couldn't query API for $IP. Error: $($_.Exception.Message)"
            }
        } else {
            Write-Verbose -Message "Find-IPGeolocation - Using cached data for $IP"
            $Script:CachedGEO[$IP]
        }
    }
}