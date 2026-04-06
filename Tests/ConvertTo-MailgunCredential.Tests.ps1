Describe 'ConvertTo-MailgunCredential cmdlet' {
    It 'accepts ApiKeySecureString and returns a PSCredential' {
        $secret = ConvertTo-SecureString 'mailgun-secret' -AsPlainText -Force

        $credential = ConvertTo-MailgunCredential -ApiKeySecureString $secret

        $credential | Should -BeOfType ([pscredential])
        $credential.UserName | Should -Be 'Mailgun'
        $credential.GetNetworkCredential().Password | Should -Be 'mailgun-secret'
    }

    It 'accepts SecretName and resolves the API key via Get-Secret' {
        function Get-Secret {
            param(
                [string] $Name,
                [string] $Vault
            )

            $script:LastVaultName = $Vault
            if ($Name -ne 'mailgun-key') {
                throw "Unexpected secret name: $Name"
            }

            'mailgun-secret-from-vault'
        }

        try {
            $credential = ConvertTo-MailgunCredential -SecretName 'mailgun-key' -VaultName 'LocalVault'

            $credential.GetNetworkCredential().Password | Should -Be 'mailgun-secret-from-vault'
            $script:LastVaultName | Should -Be 'LocalVault'
        } finally {
            Remove-Item Function:\Get-Secret -ErrorAction SilentlyContinue
            Remove-Variable LastVaultName -Scope Script -ErrorAction SilentlyContinue
        }
    }
}
