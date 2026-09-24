BeforeAll {
    if ($env:MAILOZAURR_TEST_MODULE_PATH) {
        Import-Module $env:MAILOZAURR_TEST_MODULE_PATH -Force
    } else {
        $env:MAILOZAURR_DEVELOPMENT = '1'
        Import-Module (Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1") -Force
    }
}

Describe 'Send-EmailMessage Graph errors' {
    BeforeEach {
        function global:Invoke-MgGraphRequest {
            [CmdletBinding()]
            param([string] $Method, [string] $Uri, [string] $ContentType, [object] $Body)
            Write-Error -Message 'Simulated Graph 404' -ErrorId GraphNotFound
        }
        $arguments = @{
            From           = 'sender@example.com'
            To             = 'recipient@example.com'
            Subject        = 'Graph error reporting'
            Text           = 'Test message'
            MgGraphRequest = $true
            Confirm        = $false
        }
    }

    AfterEach {
        Remove-Item function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
    }

    It 'does not report success when Invoke-MgGraphRequest writes an error' {
        $output = @(Send-EmailMessage @arguments -ErrorAction Continue 2>&1)
        $result = $output | Where-Object { $_ -is [Mailozaurr.SmtpResult] }
        $errors = $output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }

        $result.Status | Should -BeFalse
        $result.Error | Should -Match 'Simulated Graph 404'
        $errors[-1].FullyQualifiedErrorId | Should -Match 'GraphSendFailed'
    }

    It 'honors ErrorAction Stop' {
        { Send-EmailMessage @arguments -ErrorAction Stop | Out-Null } | Should -Throw -ExpectedMessage '*Simulated Graph 404*'
    }

    It 'reports the error when output is suppressed' {
        $output = @(Send-EmailMessage @arguments -Suppress -ErrorAction Continue 2>&1)
        @($output | Where-Object { $_ -is [Mailozaurr.SmtpResult] }).Count | Should -Be 0
        @($output | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }).Count | Should -BeGreaterThan 0
    }
}
