function ConvertTo-GraphCredential {
    [cmdletBinding(DefaultParameterSetName = 'ClearText')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'ClearText')]
        [Parameter(Mandatory, ParameterSetName = 'Encrypted')]
        [string] $ClientID,
        [Parameter(Mandatory, ParameterSetName = 'ClearText')]
        [string] $ClientSecret,
        [Parameter(Mandatory, ParameterSetName = 'Encrypted')]
        [string] $ClientSecretEncrypted,
        [Parameter(Mandatory, ParameterSetName = 'ClearText')]
        [Parameter(Mandatory, ParameterSetName = 'Encrypted')]
        [Parameter(Mandatory)][string] $DirectoryID
    )
    # Convert to SecureString
    Try {
        if ($ClientSecretEncrypted) {
            $EncryptedToken = ConvertTo-SecureString -String $ClientSecretEncrypted -ErrorAction Stop
        } else {
            $EncryptedToken = ConvertTo-SecureString -String $ClientSecret -AsPlainText -Force -ErrorAction Stop
        }
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
        } else {
            Write-Warning "ConvertTo-GraphCredential - Error: $($_.Exception.Message)"
            return
        }
    }
    $UserName = -join ($ClientID, '@', $DirectoryID)
    $EncryptedCredentials = [System.Management.Automation.PSCredential]::new($UserName, $EncryptedToken)
    $EncryptedCredentials
}