function Find-DNSSECRecord {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory, ValueFromPipelineByPropertyName, ValueFromPipeline, Position = 0)][Array] $DomainName,
        [string] $DnsServer,
        [ValidateSet('Cloudflare', 'Google')][string] $DNSProvider,
        [switch] $AsHashTable
    )
    begin {
        $DNSSECAlgorithmNames = [ordered] @{
            '1'  = @{ Short = 'RSAMD5'; Long = 'RSA/MD5' }
            '2'  = @{ Short = 'DH'; Long = 'Diffie-Hellman' }
            '3'  = @{ Short = 'DSASHA1'; Long = 'DSA/SHA1' }
            '5'  = @{ Short = 'RSASHA1'; Long = 'RSA/SHA-1' }
            '6'  = @{ Short = 'DSANSEC3SHA1'; Long = 'DSA-NSEC3-SHA1' }
            '7'  = @{ Short = 'RSASHA1NSEC3SHA1'; Long = 'RSASHA1-NSEC3-SHA1' }
            '8'  = @{ Short = 'RSASHA256'; Long = 'RSA/SHA-256' }
            '10' = @{ Short = 'RSASHA512'; Long = 'RSA/SHA-512' }
            '12' = @{ Short = 'ECCGOST'; Long = 'GOST R 34.10-2001' }
            '13' = @{ Short = 'ECDSAP256SHA256'; Long = 'ECDSA Curve P-256 with SHA-256' }
            '14' = @{ Short = 'ECAP384SHA384'; Long = 'ECDSA Curve P-384 with SHA-384' }
            '15' = @{ Short = 'ED25519'; Long = 'Ed25519' }
            '16' = @{ Short = 'ED448'; Long = 'Ed448' }
        }
        $DNSSECAlgorithmNamesShortToLong = [ordered] @{
            'RSAMD5'           = 'RSA/MD5'
            'DH'               = 'Diffie-Hellman'
            'DSASHA1'          = 'DSA/SHA1'
            'RSASHA1'          = 'RSA/SHA-1'
            'DSANSEC3SHA1'     = 'DSA-NSEC3-SHA1'
            'RSASHA1NSEC3SHA1' = 'RSASHA1-NSEC3-SHA1'
            'RSASHA256'        = 'RSA/SHA-256'
            'RSASHA512'        = 'RSA/SHA-512'
            'ECCGOST'          = 'GOST R 34.10-2001'
            'ECDSAP256SHA256'  = 'ECDSA Curve P-256 with SHA-256'
            'ECAP384SHA384'    = 'ECDSA Curve P-384 with SHA-384'
            'ED25519'          = 'Ed25519'
            'ED448'            = 'Ed448'
        }
    }
    process {
        foreach ($Domain in $DomainName) {
            if ($Domain -is [string]) {
                $D = $Domain
            } elseif ($Domain -is [System.Collections.IDictionary]) {
                $D = $Domain.DomainName
                if (-not $D) {
                    Write-Warning 'Find-DNSSECRecord - property DomainName is required when passing Array of Hashtables'
                }
            }
            $Splat = @{
                Name        = "$D"
                Type        = 'DNSKEY'
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

                if ($DNSProvider -eq 'Cloudflare') {
                    #Name      Count TimeToLive Text
                    #----      ----- ---------- ----
                    #evotec.pl    48       3600 256 3 ECDSAP256SHA256 oJMRESz5E4gYzS/q6XDrvU1qMPYIjCWzJaOau8XNEZeqCYKD5ar0IRd8KqXXFJkqmVfRvMGPmM1x8fGAa2XhSA==
                    #evotec.pl    48       3600 257 3 ECDSAP256SHA256 mdsswUyr3DPW132mOi8V9xESWE8jTo0dxCjjnopKl+GqJxpVXckHAeF+KkxLbxILfDLUT0rAK9iUzy1L53eKGQ==
                    foreach ($Record in $DNSRecordAnswers) {
                        $textParts = $Record.Text -split ' '
                        $algorithmNumber = $textParts[2]
                        $MailRecord = [ordered] @{
                            Name          = $D
                            Flags         = [int] $textParts[0]
                            Protocol      = [int] $textParts[1]
                            Algorithm     = $algorithmNumber
                            AlgorithmLong = $DNSSECAlgorithmNamesShortToLong[$algorithmNumber]
                            PublicKey     = $textParts[3..($textParts.Length - 1)] -join ' '
                            #PublicKeyAsString = $textParts[3..($textParts.Length - 1)] -join ' '
                            TimeToLive    = $Record.TimeToLive
                            QueryServer   = $DNSRecord.NameServer -join '; '
                            Advisory      = "DNSSEC record found."
                        }
                        if ($AsHashTable) {
                            $MailRecord
                        } else {
                            [PSCustomObject] $MailRecord
                        }
                    }

                } elseif ($DNSProvider -eq 'Google') {
                    #Name       Count TimeToLive Text
                    #----       ----- ---------- ----
                    #evotec.pl.    48       3397 257 3 13 mdsswUyr3DPW132mOi8V9xESWE8jTo0dxCjjnopKl+GqJxpVXckHAeF+KkxLbxILfDLUT0rAK9iUzy1L53eKGQ==
                    #evotec.pl.    48       3397 256 3 13 oJMRESz5E4gYzS/q6XDrvU1qMPYIjCWzJaOau8XNEZeqCYKD5ar0IRd8KqXXFJkqmVfRvMGPmM1x8fGAa2XhSA==
                    # $DNSRecordAnswers
                    foreach ($Record in $DNSRecordAnswers) {
                        $textParts = $Record.Text -split ' '
                        $algorithmNumber = $textParts[2]
                        $algorithmNames = $DNSSECAlgorithmNames[$algorithmNumber]
                        $MailRecord = [ordered] @{
                            Name          = $D
                            Flags         = [int] $textParts[0]
                            Protocol      = [int] $textParts[1]
                            Algorithm     = $algorithmNames.Short
                            AlgorithmLong = $algorithmNames.Long
                            PublicKey     = $textParts[3..($textParts.Length - 1)] -join ' '
                            #PublicKeyAsString = $textParts[3..($textParts.Length - 1)] -join ' '
                            TimeToLive    = $Record.TimeToLive
                            QueryServer   = $DNSRecord.NameServer -join '; '
                            Advisory      = "DNSSEC record found."
                        }
                        if ($AsHashTable) {
                            $MailRecord
                        } else {
                            [PSCustomObject] $MailRecord
                        }
                    }
                } else {
                    foreach ($Record in $DNSRecordAnswers) {
                        $MailRecord = [ordered] @{
                            Name          = $D
                            Flags         = $Record.Flags             # : 257
                            Protocol      = $Record.Protocol          # : 3
                            Algorithm     = $Record.Algorithm         # : ECDSAP256SHA256
                            AlgorithmLong = $DNSSECAlgorithmNamesShortToLong[$Record.Algorithm.ToString()]
                            PublicKey     = $Record.PublicKeyAsString # : mdsswUyr3DPW132mOi8V9xESWE8jTo0dxCjjnopKl+GqJxpVXckHAeF+KkxLbxILfDLUT0rAK9iUzy1L53eKGQ==
                            TimeToLive    = $Record.TimeToLive
                            QueryServer   = $DNSRecord.NameServer
                            Advisory      = "DNSSEC record found."
                        }
                        if ($AsHashTable) {
                            $MailRecord
                        } else {
                            [PSCustomObject] $MailRecord
                        }
                    }
                }
                if ($DNSRecordAnswers.Count -eq 0) {
                    $MailRecord = [ordered] @{
                        Name          = $D
                        Flags         = ''
                        Protocol      = ''
                        Algorithm     = ''
                        AlgorithmLong = ''
                        PublicKey     = ''
                        TimeToLive    = ''
                        QueryServer   = $DNSRecord.NameServer
                        Advisory      = "No DNSSEC record found."
                    }
                    if ($AsHashTable) {
                        $MailRecord
                    } else {
                        [PSCustomObject] $MailRecord
                    }
                }
            } catch {
                $MailRecord = [ordered] @{
                    Name          = $D
                    Flags         = ''
                    Protocol      = ''
                    Algorithm     = ''
                    AlgorithmLong = ''
                    PublicKey     = ''
                    TimeToLive    = ''
                    QueryServer   = if ($DNSRecord.NameServer) { $DNSRecord.NameServer } elseif ($DNSProvider) { $DNSProvider } else { $DnsServer }
                    Advisory      = "No DNSSEC record found."
                }
                if ($AsHashTable) {
                    $MailRecord
                } else {
                    [PSCustomObject] $MailRecord
                }
                Write-Warning "Find-DNSSECRecord - $_"
            }

        }
    }
}