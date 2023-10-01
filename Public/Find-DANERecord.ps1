function Find-DANERecord {
    <#
    .SYNOPSIS
    Queries DNS to provide DANE information

    .DESCRIPTION
    Queries DNS to provide DANE information

    .PARAMETER DomainName
    Name/DomainName to query for DANE record (converts to MX record automatically)

    .PARAMETER DnsServer
    Allows to choose DNS IP address to ask for DNS query. By default uses system ones.

    .PARAMETER DNSProvider
    Allows to choose DNS Provider that will be used for HTTPS based DNS query (Cloudlare or Google)

    .PARAMETER AsHashTable
    Returns Hashtable instead of PSCustomObject

    .PARAMETER AsObject
    Returns an object rather than string based represantation for name servers (for easier display purposes)

    .EXAMPLE
    Find-DaneRecord -DomainName 'ietf.org' -DNSProvider Google | Format-Table

    .EXAMPLE
    Find-DaneRecord -DomainName 'ietf.org' -DNSProvider Cloudflare | Format-Table

    .EXAMPLE
    Find-DaneRecord -DomainName 'evotec.xyz' | Format-Table

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
        $MxRecords = Find-MxRecord -DomainName $DomainName
    }
    process {
        If (-not $MxRecords.MX) {
            $MailRecord = [ordered] @{
                Name                       = $MXRecords.Name
                Status                     = $false
                Count                      = 0
                Record                     = ''
                TheCertificateUsageField   = ''
                SelectorField              = ''
                MatchingTypeField          = ''
                CertificateAssociationData = ''
                DANE                       = ''
                TimeToLive                 = ''
                QueryServer                = ''
            }
            Write-Warning -Message "Find-DANERecord - Unable to found MX record for '$($MXRecords.Name)'"
        } else {
            foreach ($Domain in $MxRecords.MX) {
                $D = $Domain
                $Splat = @{
                    Name        = "_25._tcp.$D"
                    Type        = 'TLSA'
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
                    [Array] $DNSRecordAnswers = $DNSRecord.Answers
                    if ($DNSRecordAnswers.Count -eq 1) {
                        if ($DNSRecordAnswers.Text) {
                            if ($DNSProvider -eq 'Cloudflare') {
                                $DataToAnalyze = Convert-CloudFlareToGoogleDane -DaneRecord $DNSRecordAnswers.Text
                                $Record = $DNSRecordAnswers.Name
                            } else {
                                $DataToAnalyze = $DNSRecordAnswers.Text
                                $Record = $DNSRecordAnswers.Name
                            }
                            $DANEData = $DataToAnalyze -split " "
                            $DANEData = @(
                                # we are translating data to be similar to DNSClient output
                                [DnsClient.Protocol.TlsaCertificateUsage] $DANEData[0]
                                [DnsClient.Protocol.TlsaSelector] $DANEData[1]
                                [DnsClient.Protocol.TlsaMatchingType] $DANEData[2]
                                $DANEData[3]
                            )
                        } else {
                            # Proper DANE record delivered by DNSClient contains translation of the data
                            $DANEData = @(
                                $DNSRecordAnswers[0].CertificateUsage,
                                $DNSRecordAnswers[0].Selector,
                                $DNSRecordAnswers[0].MatchingType,
                                $DNSRecordAnswers[0].CertificateAssociationDataAsString
                            )
                            $Record = $DNSRecordAnswers.DomainName
                        }
                    } else {
                        $DANEData = @()
                        $Record = $DNSRecordAnswers.Name
                    }
                    # https://en.wikipedia.org/wiki/DNS-based_Authentication_of_Named_Entities
                    if (-not $AsObject) {
                        $MailRecord = [ordered] @{
                            Name                       = $D
                            Status                     = if ($DANEData.Count -eq 4) { $true } else { $false }
                            Count                      = $DNSRecordAnswers.Count
                            Record                     = $Record

                            # Certificate usage
                            # The first field after the TLSA text in the DNS RR, specifies how to verify the certificate.
                            # A value of 0 is for what is commonly called CA constraint (and PKIX-TA). The certificate provided when establishing TLS must be issued by the listed root-CA or one of its intermediate CAs, with a valid certification path to a root-CA already trusted by the application doing the verification. The record may just point to an intermediate CA, in which case the certificate for this service must come via this CA, but the entire chain to a trusted root-CA must still be valid.[a]
                            # A value of 1 is for what is commonly called service certificate constraint (and PKIX-EE). The certificate used must match the TLSA record, and it must also pass PKIX certification path validation to a trusted root-CA.
                            # A value of 2 is for what is commonly called trust anchor assertion (and DANE-TA). The TLSA record matches the certificate of the root CA, or one of the intermediate CAs, of the certificate in use by the service. The certification path must be valid up to the matching certificate, but there is no need for a trusted root-CA.
                            # A value of 3 is for what is commonly called domain issued certificate (and DANE-EE). The TLSA record matches the used certificate itself. The used certificate does not need to be signed by other parties. This is useful for self-signed certificates, but also for cases where the validator does not have a list of trusted root certificates.

                            CertificateUsage           = $DANEData[0]

                            # Selector
                            # When connecting to the service and a certificate is received, the selector field specifies which parts of it should be checked.
                            # A value of 0 means to select the entire certificate for matching.
                            # A value of 1 means to select just the public key for certificate matching. Matching the public key is often sufficient, as this is likely to be unique.
                            SelectorField              = $DANEData[1]

                            # Matching type
                            # A type of 0 means the entire information selected is present in the certificate association data.
                            # A type of 1 means to do a SHA - 256 hash of the selected data.
                            # A type of 2 means to do a SHA - 512 hash of the selected data.
                            MatchingTypeField          = $DANEData[2]

                            # The actual data to be matched given the settings of the other fields. This is a long "text string" of hexadecimal data.
                            CertificateAssociationData = if ($DANEData[3]) { $DANEData[3].ToLower() } else { '' }
                            DANE                       = $DNSRecordAnswers.Text -join '; '
                            TimeToLive                 = $DNSRecordAnswers.TimeToLive -join '; '
                            QueryServer                = $DNSRecord.NameServer -join '; '
                        }
                    } else {
                        $MailRecord = [ordered] @{
                            Name                       = $D
                            Status                     = if ($DANEData.Count -eq 4) { $true } else { $false }
                            Count                      = $DNSRecordAnswers.Count
                            Record                     = $DNSRecordAnswers.Name
                            CertificateUsage           = $DANEData[0]
                            SelectorField              = $DANEData[1]
                            MatchingTypeField          = $DANEData[2]
                            CertificateAssociationData = $DANEData[3]
                            DANE                       = $DNSRecordAnswers.Text
                            TimeToLive                 = $DNSRecordAnswers.TimeToLive
                            QueryServer                = $DNSRecord.NameServer
                        }
                    }
                } catch {
                    $MailRecord = [ordered] @{
                        Name                       = $D
                        Status                     = $false
                        Count                      = 0
                        Record                     = ''
                        TheCertificateUsageField   = ''
                        SelectorField              = ''
                        MatchingTypeField          = ''
                        CertificateAssociationData = ''
                        DANE                       = ''
                        TimeToLive                 = ''
                        QueryServer                = ''
                    }
                    Write-Warning -Message "Find-DANERecord - $_"
                }

            }
        }
        if ($AsHashTable) {
            $MailRecord
        } else {
            [PSCustomObject] $MailRecord
        }
    }
}