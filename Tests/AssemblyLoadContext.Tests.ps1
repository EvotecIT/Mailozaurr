Describe 'Packaged AssemblyLoadContext isolation' {
    It 'loads binary cmdlets, package dependencies, and allowlisted public types from the module ALC' {
        if ($PSVersionTable.PSEdition -ne 'Core') {
            Set-ItResult -Skipped -Because 'module-scoped AssemblyLoadContext is PowerShell Core-only'
            return
        }

        $packagedModule = Join-Path $PSScriptRoot '..\Artefacts\Unpacked\Modules\Mailozaurr'
        $packagedManifest = (Resolve-Path -LiteralPath (Join-Path $packagedModule 'Mailozaurr.psd1')).ProviderPath
        $expectedCommandAssemblyPath = (Resolve-Path -LiteralPath (Join-Path $packagedModule 'Lib\Core\Mailozaurr.PowerShell.dll')).ProviderPath
        $expectedIsolatedDependencyNames = @(
            'System.Formats.Asn1'
            'System.Security.Cryptography.Pkcs'
            'System.Security.Cryptography.ProtectedData'
        )
        $expectedCorePath = (Resolve-Path -LiteralPath (Join-Path $packagedModule 'Lib\Core')).ProviderPath
        foreach ($dependencyName in $expectedIsolatedDependencyNames) {
            $null = Get-Item -LiteralPath (Join-Path $expectedCorePath "$dependencyName.dll") -ErrorAction Stop
        }

        $moduleManifestLiteral = $packagedManifest.Replace("'", "''")
        $script = @"
`$ErrorActionPreference = 'Stop'
`$WarningPreference = 'SilentlyContinue'
Import-Module '$moduleManifestLiteral' -Force

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
`$securePassword = ConvertTo-SecureString 'mailozaurr-regression' -AsPlainText -Force
`$protectedPassword = ConvertFrom-SecureString `$securePassword
`$plainPassword = `$smtp.ConvertSecureStringToPlainString(`$protectedPassword, `$true)
`$imapMessage = [Mailozaurr.ImapEmailMessage]::new([MailKit.UniqueId]::MinValue, `$message)
`$pop3Message = [Mailozaurr.Pop3EmailMessage]::new(0, `$message)
`$pendingMessage = [Mailozaurr.PendingMessageRecord]::new()
`$pendingMessage.MessageId = 'import-boundary'
`$isolatedDependencyNames = @(
    'System.Formats.Asn1'
    'System.Security.Cryptography.Pkcs'
    'System.Security.Cryptography.ProtectedData'
)
foreach (`$dependencyName in `$isolatedDependencyNames) {
    `$dependencyAssemblyName = [System.Reflection.AssemblyName]::new(`$dependencyName)
    `$null = `$commandAlc.LoadFromAssemblyName(`$dependencyAssemblyName)
}
`$isolatedDependencies = [ordered] @{}
foreach (`$dependencyName in `$isolatedDependencyNames) {
    `$dependencyAssembly = @(
        [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object {
            `$_.GetName().Name -eq `$dependencyName -and
            [object]::ReferenceEquals(
                [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$_),
                `$commandAlc
            )
        }
    ) | Select-Object -First 1
    `$isolatedDependencies[`$dependencyName] = [pscustomobject] @{
        Path = `$dependencyAssembly.Location
        Version = `$dependencyAssembly.GetName().Version.ToString()
        ALC = [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$dependencyAssembly).Name
        ALCIsDefault = [object]::ReferenceEquals(
            [System.Runtime.Loader.AssemblyLoadContext]::GetLoadContext(`$dependencyAssembly),
            [System.Runtime.Loader.AssemblyLoadContext]::Default
        )
    }
}
`$expectedAllowedTypes = @(
    'MailKit.Net.Pop3.Pop3Client'
    'MailKit.Net.Smtp.SmtpCapability'
    'MailKit.Security.SecureSocketOptions'
    'MailKit.UniqueId'
    'Mailozaurr.ClientSmtp'
    'Mailozaurr.Definitions.AttachmentStreamStagingOptions'
    'Mailozaurr.Definitions.EmailMessageContent'
    'Mailozaurr.Definitions.StreamAttachmentDescriptor'
    'Mailozaurr.EmailEncryption'
    'Mailozaurr.EmailMessage'
    'Mailozaurr.EmailProvider'
    'Mailozaurr.FilePendingMessageRepository'
    'Mailozaurr.FileSentMessageRepository'
    'Mailozaurr.Graph'
    'Mailozaurr.GraphApiErrorParser'
    'Mailozaurr.GraphAttachment'
    'Mailozaurr.GraphContent'
    'Mailozaurr.GraphEmailMessage'
    'Mailozaurr.GraphEvent'
    'Mailozaurr.GraphHttpMethod'
    'Mailozaurr.GraphMailboxFolderStatistics'
    'Mailozaurr.GraphMailboxPermission'
    'Mailozaurr.GraphMessage'
    'Mailozaurr.GraphMessageInfo'
    'Mailozaurr.GraphSendPolicy'
    'Mailozaurr.GraphUploadSessionResult'
    'Mailozaurr.HtmlUtils'
    'Mailozaurr.ImapEmailMessage'
    'Mailozaurr.ImapMessageInfo'
    'Mailozaurr.LoggingMessages'
    'Mailozaurr.MailozaurrOptions'
    'Mailozaurr.MailFileAddress'
    'Mailozaurr.MailFileAttachment'
    'Mailozaurr.MailFileFormat'
    'Mailozaurr.MailFileMessage'
    'Mailozaurr.MailFileReader'
    'Mailozaurr.MailFileReaderOptions'
    'Mailozaurr.MailFileRecipient'
    'Mailozaurr.MailFileRecipientType'
    'Mailozaurr.MicrosoftGraphUtils'
    'Mailozaurr.NonDeliveryReports.NonDeliveryReport'
    'Mailozaurr.PendingMessageRecord'
    'Mailozaurr.PendingMessageRepositoryOptions'
    'Mailozaurr.PendingMessageSenderFactory'
    'Mailozaurr.Pop3Connector'
    'Mailozaurr.Pop3EmailMessage'
    'Mailozaurr.Pop3MessageInfo'
    'Mailozaurr.SendLogResolver'
    'Mailozaurr.SentMessageRecord'
    'Mailozaurr.Smtp'
    'Mailozaurr.SmtpConnectionPool'
    'Mailozaurr.SmtpConnectionInfo'
    'Mailozaurr.SmtpDeliverySession'
    'Mailozaurr.SmtpResult'
    'Mailozaurr.TemporarySmimeCertificate'
    'MimeKit.BodyBuilder'
    'MimeKit.MailboxAddress'
    'MimeKit.MimeMessage'
    'MimeKit.TextPart'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookReaderOptions'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookObjectType'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookSearchCheckpoint'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookSearchFields'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookSearchMatchMode'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookSearchReport'
    'OfficeIMO.Email.AddressBook.OfflineAddressBookSearchResult'
    'OfficeIMO.Email.ContentLineReaderOptions'
    'OfficeIMO.Email.Data.EmailDataArtifactKind'
    'OfficeIMO.Email.Data.EmailDataOpenOptions'
    'OfficeIMO.Email.Data.EmailDataOpenResult'
    'OfficeIMO.Email.EmailReaderOptions'
    'OfficeIMO.Email.EmailSemanticComparisonOptions'
    'OfficeIMO.Email.EmailSemanticComparisonReport'
    'OfficeIMO.Email.EmailSemanticFingerprint'
    'OfficeIMO.Email.OutlookItemKind'
    'OfficeIMO.Email.Store.EmailStoreContentMatchMode'
    'OfficeIMO.Email.Store.EmailConversationGraph'
    'OfficeIMO.Email.Store.EmailConversationGraphOptions'
    'OfficeIMO.Email.Store.EmailStoreContentSearchCheckpoint'
    'OfficeIMO.Email.Store.EmailStoreContentSearchFields'
    'OfficeIMO.Email.Store.EmailStoreFormat'
    'OfficeIMO.Email.Store.EmailStoreItemReadParts'
    'OfficeIMO.Email.Store.EmailStoreMergeFolderMode'
    'OfficeIMO.Email.Store.EmailStoreMaintenancePlan'
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
        if (`$entry.Key -notlike 'Mailozaurr.*' -and `$entry.Key -notlike 'OfficeIMO.Email.*' -and
            `$entry.Key -notlike 'MailKit.*' -and `$entry.Key -notlike 'MimeKit.*') {
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
    `$null = [type]'Mailozaurr.PendingMessageProcessor'
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
    SecurePasswordRoundTrip = `$plainPassword
    ImapMessageType = `$imapMessage.GetType().FullName
    Pop3MessageType = `$pop3Message.GetType().FullName
    PendingMessageId = `$pendingMessage.MessageId
    IsolatedDependencies = `$isolatedDependencies
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
        $result.SecurePasswordRoundTrip | Should -Be 'mailozaurr-regression'
        $result.ImapMessageType | Should -Be 'Mailozaurr.ImapEmailMessage'
        $result.Pop3MessageType | Should -Be 'Mailozaurr.Pop3EmailMessage'
        $result.PendingMessageId | Should -Be 'import-boundary'
        foreach ($dependencyName in $expectedIsolatedDependencyNames) {
            $dependency = $result.IsolatedDependencies.$dependencyName
            ($dependency.Path -replace '\\', '/') | Should -BeLike "$(($expectedCorePath -replace '\\', '/'))/*"
            $dependency.Version | Should -Be '10.0.0.0'
            $dependency.ALC | Should -Be 'Mailozaurr'
            $dependency.ALCIsDefault | Should -BeFalse
        }
        $result.AllowedTypeCount | Should -Be 88
        $result.ActualAllowedTypeCount | Should -Be 88
        @($result.MissingAllowedTypes).Count | Should -Be 0
        @($result.UnexpectedAllowedTypes).Count | Should -Be 0
        @($result.WrongAllowedTypeContexts).Count | Should -Be 0
        $result.UnlistedTypeVisibleByName | Should -BeFalse
    }
}
