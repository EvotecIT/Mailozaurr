Describe 'ConvertTo-GraphCredential cmdlet' {
    It 'accepts ClientSecretSecureString and returns a PSCredential' {
        $secret = ConvertTo-SecureString 'graph-secret' -AsPlainText -Force

        $credential = ConvertTo-GraphCredential -ClientId 'client' -ClientSecretSecureString $secret -DirectoryId 'tenant'

        $credential | Should -BeOfType ([pscredential])
        $credential.UserName | Should -Be 'client@tenant'
        $credential.GetNetworkCredential().Password | Should -Be 'graph-secret'
    }

    It 'accepts SecretName and resolves the client secret via Get-Secret' {
        function Get-Secret {
            param(
                [string] $Name,
                [string] $Vault
            )

            $script:LastVaultName = $Vault
            if ($Name -ne 'graph-client-secret') {
                throw "Unexpected secret name: $Name"
            }

            ConvertTo-SecureString 'graph-secret-from-vault' -AsPlainText -Force
        }

        try {
            $credential = ConvertTo-GraphCredential -ClientId 'client' -DirectoryId 'tenant' -SecretName 'graph-client-secret' -VaultName 'LocalVault'

            $credential.GetNetworkCredential().Password | Should -Be 'graph-secret-from-vault'
            $script:LastVaultName | Should -Be 'LocalVault'
        } finally {
            Remove-Item Function:\Get-Secret -ErrorAction SilentlyContinue
            Remove-Variable LastVaultName -Scope Script -ErrorAction SilentlyContinue
        }
    }
}
