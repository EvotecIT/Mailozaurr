function ConvertFrom-OAuth2Credential {
    [cmdletBinding()]
    param(
        [alias('oAuth')][Parameter(Mandatory)][PSCredential] $Credential
    )
    [PSCustomObject] @{
        UserName = $Credential.UserName
        Token    = $Credential.GetNetworkCredential().Password
    }
}