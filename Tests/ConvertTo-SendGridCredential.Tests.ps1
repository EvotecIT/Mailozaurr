Describe 'ConvertTo-SendGridCredential cmdlet' {
    It 'accepts ApiKeySecureString and returns a PSCredential' {
        $secret = ConvertTo-SecureString 'sendgrid-secret' -AsPlainText -Force

        $credential = ConvertTo-SendGridCredential -ApiKeySecureString $secret

        $credential | Should -BeOfType ([pscredential])
        $credential.UserName | Should -Be 'SendGrid'
        $credential.GetNetworkCredential().Password | Should -Be 'sendgrid-secret'
    }

    It 'accepts SecretName and resolves the API key via Get-Secret' {
        function Get-Secret {
            param(
                [string] $Name,
                [string] $Vault
            )

            $script:LastVaultName = $Vault
            if ($Name -ne 'sendgrid-key') {
                throw "Unexpected secret name: $Name"
            }

            'sendgrid-secret-from-vault'
        }

        try {
            $credential = ConvertTo-SendGridCredential -SecretName 'sendgrid-key' -VaultName 'LocalVault'

            $credential.GetNetworkCredential().Password | Should -Be 'sendgrid-secret-from-vault'
            $script:LastVaultName | Should -Be 'LocalVault'
        } finally {
            Remove-Item Function:\Get-Secret -ErrorAction SilentlyContinue
            Remove-Variable LastVaultName -Scope Script -ErrorAction SilentlyContinue
        }
    }
}
