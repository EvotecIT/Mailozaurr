function ConvertTo-SendGridCredential {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][string] $ApiKey
    )
    # Convert to SecureString
    $EncryptedToken = ConvertTo-SecureString -String $ApiKey -AsPlainText -Force
    $EncryptedCredentials = [System.Management.Automation.PSCredential]::new('SendGrid', $EncryptedToken)
    $EncryptedCredentials
}