function ConvertFrom-GraphCredential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCredential] $Credential
    )
    if ($Credential.UserName -eq 'MSAL') {
        [PSCustomObject] @{
            ClientID     = 'MSAL'
            ClientSecret = $Credential.GetNetworkCredential().Password
        }
    } else {
        $Object = $Credential.UserName -split '@'
        if ($Object.Count -eq 2) {
            [PSCustomObject] @{
                ClientID     = $Object[0]
                DirectoryID  = $Object[1]
                ClientSecret = $Credential.GetNetworkCredential().Password
            }
        }
    }
}