function Find-SecurityTxtRecord {
    <#
    .SYNOPSIS
    Queries website to provide security.txt information

    .DESCRIPTION
    Queries website to provide security.txt information

    .PARAMETER DomainName
    Domain Name to query for security.txt record

    .EXAMPLE
    Find-SecurityTxtRecord -DomainName 'evotec.xyz', 'evotec.pl', 'google.com', 'facebook.com', 'www.gemini.com' | Format-Table -AutoSize *

    .NOTES
    General notes
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName
    )

    process {
        foreach ($Domain in $DomainName) {
            $Response = $null
            $DataFound = $false
            $DataOutput = [ordered]@{
                Domain                 = $Domain
                RecordPresent          = $false
                PGPSigned              = $false # but not verified, we would need to use PSGPG module for that
                FallbackUsed           = $false
                Url                    = $null
                ContactEmail           = [System.Collections.Generic.List[string]]::new()
                ContactWebsite         = [System.Collections.Generic.List[string]]::new()
                ContactOther           = [System.Collections.Generic.List[string]]::new()
                Acknowledgments        = [System.Collections.Generic.List[string]]::new()
                'Preferred-Languages'  = [System.Collections.Generic.List[string]]::new()
                Canonical              = [System.Collections.Generic.List[string]]::new()
                Expires                = [System.Collections.Generic.List[string]]::new()
                Encryption             = [System.Collections.Generic.List[string]]::new()
                Policy                 = [System.Collections.Generic.List[string]]::new()
                Hiring                 = [System.Collections.Generic.List[string]]::new()
                'Signature-Encryption' = [System.Collections.Generic.List[string]]::new()
            }

            $Url = "https://$Domain/.well-known/security.txt"
            try {
                $Response = Invoke-RestMethod -Uri $Url -ErrorAction Stop
                $DataFound = $true
            } catch {
                Write-Warning -Message "Find-SecurityTxtRecord - Error when getting data from $($Url): $($_.Exception.message)"
            }
            if (-not $DataFound) {
                $Url = "http://$Domain/security.txt"
                try {
                    $Response = Invoke-RestMethod -Uri $Url -ErrorAction Stop
                    $DataOutput['FallbackUsed'] = $true
                    $DataFound = $true
                } catch {
                    Write-Warning -Message "Find-SecurityTxtRecord - Error when getting data from $($Url): $($_.Exception.message)"
                }
            }
            if ($DataFound) {
                $DataOutput['Url'] = $Url
                $ResponseData = $Response.Trim() -split "`r`n" -split "`n"
                $DataOutput['RecordPresent'] = $true
                foreach ($Data in $ResponseData) {
                    if ($Data.StartsWith("Contact:")) {
                        $Value = $Data.Replace("Contact:", "").Trim()
                        if ($Value -match "mailto:") {
                            $Value = $Value.Replace("mailto:", "")
                            $DataOutput['ContactEmail'].Add($Value)
                        } elseif ($Value -like "*://*") {
                            $DataOutput['ContactWebsite'].Add($Value)
                        } elseif ($Value -like "*@*") {
                            $DataOutput['ContactEmail'].Add($Value)
                        } else {
                            $DataOutput['ContactWebsite'].Add($Value)
                        }
                    } elseif ($Data.StartsWith("Acknowledgments:")) {
                        $DataOutput['Acknowledgments'].Add($Data.Replace("Acknowledgments:", "").Trim())
                    } elseif ($Data.StartsWith("Preferred-Languages:")) {
                        $DataOutput['Preferred-Languages'].Add($Data.Replace("Preferred-Languages:", "").Trim())
                    } elseif ($Data.StartsWith("Canonical:")) {
                        $DataOutput['Canonical'].Add($Data.Replace("Canonical:", "").Trim())
                    } elseif ($Data.StartsWith("Expires:")) {
                        $DataOutput['Expires'].Add($Data.Replace("Expires:", "").Trim())
                    } elseif ($Data.StartsWith("Encryption:")) {
                        $DataOutput['Encryption'].Add($Data.Replace("Encryption:", "").Trim())
                    } elseif ($Data.StartsWith("Policy:")) {
                        $DataOutput['Policy'].Add($Data.Replace("Policy:", "").Trim())
                    } elseif ($Data.StartsWith("Hiring:")) {
                        $DataOutput['Hiring'].Add($Data.Replace("Hiring:", "").Trim())
                    } elseif ($Data.StartsWith("Signature-Encryption:")) {
                        $DataOutput['Signature-Encryption'].Add($Data.Replace("Signature-Encryption:", "").Trim())
                    } elseif ($Data.StartsWith("-----BEGIN PGP SIGNED MESSAGE-----")) {
                        $DataOutput['PGPSigned'] = $true
                    } else {
                        # this won't really work, as some companies like gemini add full blown text
                        # https://www.gemini.com/.well-known/security.txt
                        # $DataField = $Data.Split(":")[0]
                        # $DataValue = $Data.Split(":")[1].Trim()
                        # if (-not $DataOutput[$DataField]) {
                        #     $DataOutput[$DataField] = [System.Collections.Generic.List[string]]::new()
                        # }
                        # $DataOutput[$DataField].Add($DataValue)
                    }
                }

                # Lets fix the output, to not have arrays of 1 element
                foreach ( $Key in [string[]] $DataOutput.Keys) {
                    $DataOutput[$Key] = if ($DataOutput[$Key] -is [Array] -and $DataOutput[$Key].Count -eq 1) {
                        $DataOutput[$Key][0]
                    } else {
                        $DataOutput[$Key]
                    }
                }
            }
            [PSCustomObject] $DataOutput
        }

    }
}