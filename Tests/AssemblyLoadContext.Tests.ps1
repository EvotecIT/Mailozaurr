Describe 'Packaged AssemblyLoadContext isolation' {
    It 'loads binary cmdlets and selected public types from the module ALC' {
        $packagedModuleRoot = Join-Path $PSScriptRoot '..\Artefacts\Modules'
        $packagedModule = Join-Path $packagedModuleRoot 'Mailozaurr'
        $packagedLoader = Join-Path $packagedModule 'Lib\Core\Mailozaurr.ModuleLoadContext.dll'
        if ($PSVersionTable.PSEdition -ne 'Core' -or -not (Test-Path -LiteralPath $packagedLoader)) {
            Set-ItResult -Skipped -Because 'packaged Core artifact is required'
            return
        }

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module Mailozaurr -Force

`$command = Get-Command Send-EmailMessage -Module Mailozaurr -ErrorAction Stop
`$commandAssembly = `$command.ImplementingType.Assembly
`$commandAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$commandAssembly)
`$smtpAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([Mailozaurr.Smtp].Assembly)
`$msgAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([Mailozaurr.EmailMessage].Assembly)
`$mimeAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([MimeKit.MimeMessage].Assembly)
`$mailKitAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([MailKit.UniqueId].Assembly)
`$message = [MimeKit.MimeMessage]::new()
`$smtp = [Mailozaurr.Smtp]::new()

[pscustomobject]@{
    CommandName = `$command.Name
    CommandAssembly = `$commandAssembly.GetName().Name
    CommandAssemblyPath = `$commandAssembly.Location
    CommandALC = `$commandAlc.Name
    CommandALCIsDefault = [object]::ReferenceEquals(`$commandAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    SmtpType = [Mailozaurr.Smtp].FullName
    SmtpALC = `$smtpAlc.Name
    SmtpALCIsDefault = [object]::ReferenceEquals(`$smtpAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    EmailProviderType = [Mailozaurr.EmailProvider].FullName
    EmailMessageType = [Mailozaurr.EmailMessage].FullName
    EmailMessageALC = `$msgAlc.Name
    EmailMessageALCIsDefault = [object]::ReferenceEquals(`$msgAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    MimeMessageType = `$message.GetType().FullName
    MimeMessageALC = `$mimeAlc.Name
    MimeMessageALCIsDefault = [object]::ReferenceEquals(`$mimeAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    MailKitUniqueIdType = [MailKit.UniqueId].FullName
    MailKitUniqueIdALC = `$mailKitAlc.Name
    MailKitUniqueIdALCIsDefault = [object]::ReferenceEquals(`$mailKitAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    SmtpCreated = `$null -ne `$smtp
} | ConvertTo-Json -Compress
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $output = pwsh -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)

        $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
        $json | Should -Not -BeNullOrEmpty -Because ($output -join [Environment]::NewLine)
        $result = $json | ConvertFrom-Json

        $result.CommandName | Should -Be 'Send-EmailMessage'
        $result.CommandAssembly | Should -Be 'Mailozaurr.PowerShell'
        $result.CommandAssemblyPath | Should -BeLike '*\Artefacts\Modules\Mailozaurr\Lib\Core\Mailozaurr.PowerShell.dll'
        $result.CommandALC | Should -Be 'Mailozaurr'
        $result.CommandALCIsDefault | Should -BeFalse
        $result.SmtpType | Should -Be 'Mailozaurr.Smtp'
        $result.SmtpALC | Should -Be 'Mailozaurr'
        $result.SmtpALCIsDefault | Should -BeFalse
        $result.EmailProviderType | Should -Be 'Mailozaurr.EmailProvider'
        $result.EmailMessageType | Should -Be 'Mailozaurr.EmailMessage'
        $result.EmailMessageALC | Should -Be 'Mailozaurr'
        $result.EmailMessageALCIsDefault | Should -BeFalse
        $result.MimeMessageType | Should -Be 'MimeKit.MimeMessage'
        $result.MimeMessageALC | Should -Be 'Mailozaurr'
        $result.MimeMessageALCIsDefault | Should -BeFalse
        $result.MailKitUniqueIdType | Should -Be 'MailKit.UniqueId'
        $result.MailKitUniqueIdALC | Should -Be 'Mailozaurr'
        $result.MailKitUniqueIdALCIsDefault | Should -BeFalse
        $result.SmtpCreated | Should -BeTrue
    }
}
