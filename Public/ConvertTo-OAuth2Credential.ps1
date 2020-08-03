function ConvertTo-OAuth2Credential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $UserName,
        [Parameter(Mandatory)][string] $Token
    )
    # Convert to SecureString
    $EncryptedToken = ConvertTo-SecureString -String $Token -AsPlainText -Force
    $EncryptedCredentials = [System.Management.Automation.PSCredential]::new($UserName, $EncryptedToken)
    $EncryptedCredentials
}