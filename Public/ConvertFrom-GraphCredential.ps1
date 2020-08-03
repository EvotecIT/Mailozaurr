function ConvertFrom-GraphCredential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCredential] $Credential
    )
    $Object = $Credential.UserName -split '@'
    if ($Object.Count -eq 2) {
        [PSCustomObject] @{
            ClientID     = $Object[0]
            DirectoryID  = $Object[1]
            ClientSecret = $Credential.GetNetworkCredential().Password
        }
    }
}