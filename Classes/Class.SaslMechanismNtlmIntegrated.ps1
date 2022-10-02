enum LoginState
{
    Initial
    Challenge
}
class SaslMechanismNtlmIntegrated : MailKit.Security.SaslMechanism
{

    [LoginState]$state
    [NSspi.Contexts.ClientContext]$sspiContext
 
    SaslMechanismNtlmIntegrated() : base([string]::Empty, [string]::Empty)
    {
    }

    [string]MechanismName() {
        return 'NTLM'
    }
    [string]get_MechanismName() {
        return 'NTLM'
    }
    [bool]SupportsInitialResponse()
    {
       return $true
    }

    [byte[]]Challenge(
        [byte[]]$token, 
        [int]$startIndex,
        [int]$length,
        [System.Threading.CancellationToken]$cancellationToken
        )
    {
        if ($this.IsAuthenticated) {
            throw [InvalidOperationException]::new()
        }
        $this.InitializeSSPIContext()

        [byte[]]$serverResponse = $null
        

        switch ($this.state) {
            ([LoginState]::Initial) {
                $this.sspiContext.Init($null, [ref]$serverResponse)
                $this.state = [LoginState]::Challenge
                break
            }
            ([LoginState]::Challenge) {
                $this.sspiContext.Init($token, [ref]$serverResponse)
                $this.IsAuthenticated = $true
                break
            }
            default
            {
                throw [System.IndexOutOfRangeException]::new("state")
            }
        }

        return [byte[]]$serverResponse
    }


    [void]InitializeSSPIContext()
    {
        if ($null -eq $this.sspiContext)
        {
            $credential = [NSspi.Credentials.ClientCurrentCredential]::new([NSspi.PackageNames]::Ntlm)
            $attribs = [NSspi.Contexts.ContextAttrib]::InitIntegrity,
            [NSspi.Contexts.ContextAttrib]::ReplayDetect,
            [NSspi.Contexts.ContextAttrib]::SequenceDetect,
            [NSspi.Contexts.ContextAttrib]::Confidentiality
            $this.sspiContext = [NSspi.Contexts.ClientContext]::new(
                $credential,
                [string]::Empty,
                $attribs
            )
        }
    }

    [void]Reset()
    {
        $this.state = [LoginState]::Initial
        $this.base.Reset()
    }
}