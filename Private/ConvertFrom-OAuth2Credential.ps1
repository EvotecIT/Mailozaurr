function ConvertFrom-OAuth2Credential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCredential] $Credential
    )
    [PSCustomObject] @{
        UserName = $Credential.UserName
        Token    = $Credential.GetNetworkCredential().Password
    }
}