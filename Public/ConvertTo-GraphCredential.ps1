function ConvertTo-GraphCredential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ClientID,
        [Parameter(Mandatory)][string] $ClientSecret,
        [Parameter(Mandatory)][string] $DirectoryID
    )
    # Convert to SecureString
    $EncryptedToken = ConvertTo-SecureString -String $ClientSecret -AsPlainText -Force
    $UserName = -join ($ClientID, '@', $DirectoryID)
    $EncryptedCredentials = [System.Management.Automation.PSCredential]::new($UserName, $EncryptedToken)
    $EncryptedCredentials
}