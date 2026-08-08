Describe 'Packaged AssemblyLoadContext isolation' {
    It 'loads binary cmdlets and allowlisted public types from the module ALC' {
        if ($PSVersionTable.PSEdition -ne 'Core') {
            Set-ItResult -Skipped -Because 'module-scoped AssemblyLoadContext is PowerShell Core-only'
            return
        }

        $artefactsRoot = Join-Path $PSScriptRoot '..\Artefacts'
        $packagedLoader = Get-ChildItem -LiteralPath $artefactsRoot -Filter 'Mailozaurr.ModuleLoadContext.dll' -Recurse -File | Select-Object -First 1
        $packagedLoader | Should -Not -BeNullOrEmpty -Because 'Build\Build-Module.ps1 must create the packaged ALC loader before this regression runs'
        $packagedModule = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $packagedLoader.FullName))
        $packagedModuleRoot = Split-Path -Parent $packagedModule
        $expectedCommandAssemblyPath = (Resolve-Path -LiteralPath (Join-Path $packagedModule 'Lib\Core\Mailozaurr.PowerShell.dll')).ProviderPath

        $moduleRootLiteral = $packagedModuleRoot.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
`$moduleRoot = '$moduleRootLiteral'
`$env:PSModulePath = `$moduleRoot + [IO.Path]::PathSeparator + `$env:PSModulePath

Import-Module Mailozaurr -Force

`$command = Get-Command Send-EmailMessage -Module Mailozaurr -ErrorAction Stop
`$exportCommand = Get-Command Export-MailFile -Module Mailozaurr -ErrorAction Stop
`$commandAssembly = `$command.ImplementingType.Assembly
`$commandAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$commandAssembly)
`$smtpAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([Mailozaurr.Smtp].Assembly)
`$msgAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext([Mailozaurr.EmailMessage].Assembly)
`$message = New-MimeMessage -From 'Sender <sender@example.com>' -To 'Recipient <recipient@example.com>' -Subject 'ALC' -TextBody 'Body'
`$query = New-IMAPSearchQuery -FromContains 'sender@example.com'
`$mimeAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$message.GetType().Assembly)
`$mailKitAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$query.GetType().Assembly)
`$smtp = [Mailozaurr.Smtp]::new()
`$expectedAllowedTypes = @(
    'Mailozaurr.Definitions.EmailMessageContent'
    'Mailozaurr.EmailEncryption'
    'Mailozaurr.EmailMessage'
    'Mailozaurr.EmailProvider'
    'Mailozaurr.FileSentMessageRepository'
    'Mailozaurr.GraphApiErrorParser'
    'Mailozaurr.GraphAttachment'
    'Mailozaurr.GraphContent'
    'Mailozaurr.GraphHttpMethod'
    'Mailozaurr.GraphMessage'
    'Mailozaurr.GraphSendPolicy'
    'Mailozaurr.HtmlUtils'
    'Mailozaurr.MailozaurrOptions'
    'Mailozaurr.MailFileAddress'
    'Mailozaurr.MailFileAttachment'
    'Mailozaurr.MailFileFormat'
    'Mailozaurr.MailFileMessage'
    'Mailozaurr.MailFileReader'
    'Mailozaurr.MailFileReaderOptions'
    'Mailozaurr.MailFileRecipient'
    'Mailozaurr.MailFileRecipientType'
    'Mailozaurr.SendLogResolver'
    'Mailozaurr.Smtp'
    'Mailozaurr.SmtpConnectionPool'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookReaderOptions'
    'OfficeIMO.Email.ContentLineReaderOptions'
    'OfficeIMO.Email.Data.EmailDataArtifactKind'
    'OfficeIMO.Email.Data.EmailDataOpenOptions'
    'OfficeIMO.Email.Data.EmailDataOpenResult'
    'OfficeIMO.Email.EmailReaderOptions'
    'OfficeIMO.Email.OutlookItemKind'
    'OfficeIMO.Email.Store.EmailStoreContentMatchMode'
    'OfficeIMO.Email.Store.EmailStoreContentSearchCheckpoint'
    'OfficeIMO.Email.Store.EmailStoreContentSearchFields'
    'OfficeIMO.Email.Store.EmailStoreFormat'
    'OfficeIMO.Email.Store.EmailStoreItemReadParts'
    'OfficeIMO.Email.Store.EmailStoreMergeFolderMode'
    'OfficeIMO.Email.Store.EmailStoreReaderOptions'
    'OfficeIMO.Email.Store.EmailStoreSession'
    'OfficeIMO.Email.Store.EmailStoreSpecialFolderKind'
    'OfficeIMO.Email.Store.EmailStoreValidationMode'
)
`$missingAllowedTypes = [Collections.Generic.List[string]]::new()
`$wrongAllowedTypeContexts = [Collections.Generic.List[string]]::new()
foreach (`$typeName in `$expectedAllowedTypes) {
    try {
        `$allowedType = [type] `$typeName
        `$allowedTypeAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$allowedType.Assembly)
        if (-not [object]::ReferenceEquals(`$allowedTypeAlc, `$commandAlc)) {
            `$wrongAllowedTypeContexts.Add(`$typeName)
        }
    } catch {
        `$missingAllowedTypes.Add(`$typeName)
    }
}
`$typeAccelerators = [psobject].Assembly.GetType('System.Management.Automation.TypeAccelerators')
`$getTypeAccelerators = `$typeAccelerators.GetProperty('Get', [System.Reflection.BindingFlags] 'Static,Public,NonPublic')
`$actualAllowedTypes = @(
    foreach (`$entry in `$getTypeAccelerators.GetValue(`$null).GetEnumerator()) {
        if (`$entry.Key -notlike 'Mailozaurr.*' -and `$entry.Key -notlike 'OfficeIMO.Email.*') {
            continue
        }

        `$entryAlc = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$entry.Value.Assembly)
        if ([object]::ReferenceEquals(`$entryAlc, `$commandAlc)) {
            `$entry.Key
        }
    }
)
`$unexpectedAllowedTypes = @(`$actualAllowedTypes | Where-Object { `$expectedAllowedTypes -notcontains `$_ })
`$unlistedTypeVisibleByName = `$true
try {
    `$null = [type]'Mailozaurr.MicrosoftGraphUtils'
} catch {
    `$unlistedTypeVisibleByName = `$false
}

[pscustomobject]@{
    CommandName = `$command.Name
    ExportCommandName = `$exportCommand.Name
    ExportCommandAssembly = `$exportCommand.ImplementingType.Assembly.GetName().Name
    CommandAssembly = `$commandAssembly.GetName().Name
    CommandAssemblyPath = `$commandAssembly.Location
    CommandALC = `$commandAlc.Name
    CommandALCIsDefault = [object]::ReferenceEquals(`$commandAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    SmtpType = [Mailozaurr.Smtp].FullName
    SmtpALC = `$smtpAlc.Name
    SmtpALCIsDefault = [object]::ReferenceEquals(`$smtpAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    EmailEncryptionType = [Mailozaurr.EmailEncryption].FullName
    EmailProviderType = [Mailozaurr.EmailProvider].FullName
    GraphHttpMethodType = [Mailozaurr.GraphHttpMethod].FullName
    GraphSendPolicyType = [Mailozaurr.GraphSendPolicy].FullName
    EmailMessageType = [Mailozaurr.EmailMessage].FullName
    EmailMessageALC = `$msgAlc.Name
    EmailMessageALCIsDefault = [object]::ReferenceEquals(`$msgAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    MimeMessageType = `$message.GetType().FullName
    MimeMessageALC = `$mimeAlc.Name
    MimeMessageALCIsDefault = [object]::ReferenceEquals(`$mimeAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    SearchQueryType = `$query.GetType().FullName
    SearchQueryALC = `$mailKitAlc.Name
    SearchQueryALCIsDefault = [object]::ReferenceEquals(`$mailKitAlc, [System.Runtime.Loader.AssemblyLoadContext]::Default)
    SmtpCreated = `$null -ne `$smtp
    AllowedTypeCount = `$expectedAllowedTypes.Count
    ActualAllowedTypeCount = `$actualAllowedTypes.Count
    MissingAllowedTypes = @(`$missingAllowedTypes)
    UnexpectedAllowedTypes = @(`$unexpectedAllowedTypes)
    WrongAllowedTypeContexts = @(`$wrongAllowedTypeContexts)
    UnlistedTypeVisibleByName = `$unlistedTypeVisibleByName
} | ConvertTo-Json -Compress
"@
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
        $output = pwsh -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encoded 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join [Environment]::NewLine)

        $json = $output | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith('{') } | Select-Object -Last 1
        $json | Should -Not -BeNullOrEmpty -Because ($output -join [Environment]::NewLine)
        $result = $json | ConvertFrom-Json

        $result.CommandName | Should -Be 'Send-EmailMessage'
        $result.ExportCommandName | Should -Be 'Export-MailFile'
        $result.ExportCommandAssembly | Should -Be 'Mailozaurr.PowerShell'
        $result.CommandAssembly | Should -Be 'Mailozaurr.PowerShell'
        ($result.CommandAssemblyPath -replace '\\', '/') | Should -Be ($expectedCommandAssemblyPath -replace '\\', '/')
        $result.CommandALC | Should -Be 'Mailozaurr'
        $result.CommandALCIsDefault | Should -BeFalse
        $result.SmtpType | Should -Be 'Mailozaurr.Smtp'
        $result.SmtpALC | Should -Be 'Mailozaurr'
        $result.SmtpALCIsDefault | Should -BeFalse
        $result.EmailEncryptionType | Should -Be 'Mailozaurr.EmailEncryption'
        $result.EmailProviderType | Should -Be 'Mailozaurr.EmailProvider'
        $result.GraphHttpMethodType | Should -Be 'Mailozaurr.GraphHttpMethod'
        $result.GraphSendPolicyType | Should -Be 'Mailozaurr.GraphSendPolicy'
        $result.EmailMessageType | Should -Be 'Mailozaurr.EmailMessage'
        $result.EmailMessageALC | Should -Be 'Mailozaurr'
        $result.EmailMessageALCIsDefault | Should -BeFalse
        $result.MimeMessageType | Should -Be 'MimeKit.MimeMessage'
        $result.MimeMessageALC | Should -Be 'Mailozaurr'
        $result.MimeMessageALCIsDefault | Should -BeFalse
        $result.SearchQueryType | Should -BeLike 'MailKit.Search.*SearchQuery'
        $result.SearchQueryALC | Should -Be 'Mailozaurr'
        $result.SearchQueryALCIsDefault | Should -BeFalse
        $result.SmtpCreated | Should -BeTrue
        $result.AllowedTypeCount | Should -Be 41
        $result.ActualAllowedTypeCount | Should -Be 41
        @($result.MissingAllowedTypes).Count | Should -Be 0
        @($result.UnexpectedAllowedTypes).Count | Should -Be 0
        @($result.WrongAllowedTypeContexts).Count | Should -Be 0
        $result.UnlistedTypeVisibleByName | Should -BeFalse
    }
}
