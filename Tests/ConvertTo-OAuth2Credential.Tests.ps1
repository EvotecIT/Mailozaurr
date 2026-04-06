Describe 'ConvertTo-OAuth2Credential cmdlet' {
    It 'accepts TokenSecureString and returns a PSCredential' {
        $token = ConvertTo-SecureString 'tok-secure' -AsPlainText -Force

        $credential = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -TokenSecureString $token

        $credential | Should -BeOfType ([pscredential])
        $credential.UserName | Should -Be 'user@gmail.com'
        $credential.GetNetworkCredential().Password | Should -Be 'tok-secure'
    }

    It 'accepts SecretName and resolves the token via Get-Secret' {
        function Get-Secret {
            param(
                [string] $Name,
                [string] $Vault
            )

            $script:LastVaultName = $Vault
            if ($Name -ne 'oauth-token') {
                throw "Unexpected secret name: $Name"
            }

            'tok-from-vault'
        }

        try {
            $credential = ConvertTo-OAuth2Credential -UserName 'user@gmail.com' -SecretName 'oauth-token' -VaultName 'LocalVault'

            $credential.GetNetworkCredential().Password | Should -Be 'tok-from-vault'
            $script:LastVaultName | Should -Be 'LocalVault'
        } finally {
            Remove-Item Function:\Get-Secret -ErrorAction SilentlyContinue
            Remove-Variable LastVaultName -Scope Script -ErrorAction SilentlyContinue
        }
    }
}
