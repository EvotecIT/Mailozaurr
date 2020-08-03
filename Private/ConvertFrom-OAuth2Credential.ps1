function ConvertFrom-OAuth2Credential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCredential] $OAuth2
    )
    [PSCustomObject] @{
        UserName = $OAuth2.UserName
        Token    = $OAuth2.GetNetworkCredential().Password
    }
}