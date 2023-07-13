class SaslMechanismWindowsAuth : MailKit.Security.SaslMechanism
{
    [NSspi.Contexts.ClientContext]$context
    [NSspi.Credentials.Credential]$credential
    [string]$targetServerFqdn
    [byte[]]$channelBindingToken = $null

    SaslMechanismWindowsAuth([string]$username, [string]$password, [string]$serverFqdn) : base($username, $password)
    {
        $this.targetServerFqdn = $serverFqdn

        $packageName = [NSspi.PackageNames]::Negotiate
        if ($username -and $password) {
            $domain = $null
            if ($username.Contains('\')) {
                $domain, $username = $username.Split([char[]]@('\'), 2)
            }
            $this.credential = [NSspi.Credentials.PasswordCredential]::new(
                $domain,
                $username,
                $password,
                $packageName,
                [NSspi.Credentials.CredentialUse]::Outbound)
        }
        else {
            $this.credential = [NSspi.Credentials.ClientCurrentCredential]::new($packageName)
        }   
    }

    [string]get_MechanismName() {
        return 'GSSAPI'
    }

    [bool]get_SupportsChannelBinding() {
        return $true
    }

    [bool]get_NegotiatedChannelBinding() {
        return $null -ne $this.channelBindingToken
    }

    [bool]get_IsAuthenticated() {
        return $null -ne $this.context -and $this.context.Initialized
    }

    [byte[]]Challenge(
        [byte[]]$token,
        [int]$startIndex,
        [int]$length,
        [System.Threading.CancellationToken]$cancellationToken
    ) {
        if (-not $this.context) {
            $this.context = [NSspi.Contexts.ClientContext]::new(
                $this.credential,
                "SMTPSVC/$($this.targetServerFqdn)",
                [NSspi.Contexts.ContextAttrib]'InitIntegrity, ReplayDetect, SequenceDetect, Confidentiality, MutualAuth'
            )

            [byte[]]$channelToken = $null
            if ($this.TryGetChannelBindingToken('Endpoint', [ref]$channelToken)) {
                # $channelToken is the cert hash, we still need to set the
                # required SSPI structure data that wraps it

                $endpointHeader = [System.Text.Encoding]::ASCII.GetBytes("tls-server-end-point:")

                # SEC_CHANNEL_BINDINGS
                # https://learn.microsoft.com/en-us/windows/win32/api/sspi/ns-sspi-sec_channel_bindings
                $this.channelBindingToken = [byte[]]@(
                    # Initiator
                    [System.BitConverter]::GetBytes(0)
                    [System.BitConverter]::GetBytes(0)
                    [System.BitConverter]::GetBytes(0)

                    # Acceptor
                    [System.BitConverter]::GetBytes(0)
                    [System.BitConverter]::GetBytes(0)
                    [System.BitConverter]::GetBytes(0)

                    # Application
                    [System.BitConverter]::GetBytes($endpointHeader.Length + $channelToken.Length)
                    [System.BitConverter]::GetBytes(32) # dwApplicationDataOffset
                    $endpointHeader
                    $channelToken
                )
            }
        }
        elseif ($this.context.Initialized) {
            # The subsequent token is a wrapped/encrypted SSF payload from the
            # server that indicates what SASL security layer it supports. This
            # is documented in RFC 2222 Section 7.2.2. The client needs to
            # verify the server value and send back its own.
            # FIXME: This uses a locally edited NSspi that adds the
            # DecryptStream method. The builtin Decrypt method is not suitable
            # for use with this step as it has predefined constrained on the
            # input token.
            $serverSSF = $this.context.DecryptStream($token)

            # First byte is a bitmask that denotes the SASL security layer
            #   0x1 - No security layer
            #   0x2 - Integrity protection
            #   0x4 - Privacy protection
            # We need to ensure it supports no security layer for our client
            # as MailKit does not offer a way to encrypt messages using our
            # security context (does SMTP even support such a thing).
            # The remaining values only get used with a security layer so we
            # can just return the same value back.
            if (($serverSSF[0] -band 0x1) -ne 1) {
                throw "Server does not support no SASL security layer, cannot continue"
            }
            $serverSSF[0] = 1

            $encryptedResp = $this.context.Encrypt($serverSSF)
            # Strip the NSspi header that it adds
            return $encryptedResp[8..$encryptedResp.Length]
        }

        [byte[]]$inToken = $null
        if ($length) {
            [byte[]]$inToken = [byte[]]::new($length)
            [System.Buffer]::BlockCopy($token, $startIndex, $inToken, 0, $length)
        }

        # The response value doesn't need to be checked as NSspi will raise an
        # exception on a failure.
        # This overload uses a custom local copy to support channel binding tokens
        [byte[]]$responseToken = $null
        $res = $this.context.Init($inToken, [ref]$responseToken, $this.channelBindingToken)
        if ($res -eq 'Ok') {
            $this.IsAuthenticated = $true
        }

        return $responseToken
    }
}