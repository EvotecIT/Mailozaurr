param(
    [ValidateSet('Manifest', 'Documentation', 'Build', 'Publish')]
    [string] $ConfigurationGateMode = 'Build',

    [bool] $SignModule = $true,

    [string] $ProjectBuildConfigPath = 'Build\project.build.json',

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

# Install-Module PSPublishModule -Force
Import-Module PSPublishModule -Force -ErrorAction Stop

Build-Module -ModuleName 'Mailozaurr' {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        ModuleVersion        = '3.0.X'
        # Supported PSEditions
        CompatiblePSEditions = @('Desktop', 'Core')
        # ID used to uniquely identify this module
        GUID                 = '2b0ea9f1-3ff1-4300-b939-106d5da608fa'
        # Author of this module
        Author               = 'Przemyslaw Klys'
        # Company or vendor of this module
        CompanyName          = 'Evotec'
        # Copyright statement for this module
        Copyright            = "(c) 2011 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        # Description of the functionality provided by this module
        Description          = 'PowerShell email toolkit for SMTP, IMAP, POP3, Microsoft Graph, Gmail, SendGrid, Mailgun, and Amazon SES, with message-file, PST/OST archive, signing, and encryption workflows.'
        # Minimum version of the Windows PowerShell engine required by this module
        PowerShellVersion    = '5.1'
        # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
        Tags                 = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3', 'PST', 'OST', 'OLM', 'Mbox', 'EMLX', 'Maildir')

        IconUri              = 'https://evotec.xyz/wp-content/uploads/2020/07/MailoZaurr.png'

        ProjectUri           = 'https://github.com/EvotecIT/MailoZaurr'

        #PreReleaseTag        = 'Preview5'
    }
    New-ConfigurationManifest @Manifest

    $ConfigurationFormat = [ordered] @{
        RemoveComments                              = $false

        PlaceOpenBraceEnable                        = $true
        PlaceOpenBraceOnSameLine                    = $true
        PlaceOpenBraceNewLineAfter                  = $true
        PlaceOpenBraceIgnoreOneLineBlock            = $false

        PlaceCloseBraceEnable                       = $true
        PlaceCloseBraceNewLineAfter                 = $false
        PlaceCloseBraceIgnoreOneLineBlock           = $false
        PlaceCloseBraceNoEmptyLineBefore            = $true

        UseConsistentIndentationEnable              = $true
        UseConsistentIndentationKind                = 'space'
        UseConsistentIndentationPipelineIndentation = 'IncreaseIndentationAfterEveryPipeline'
        UseConsistentIndentationIndentationSize     = 4

        UseConsistentWhitespaceEnable               = $true
        UseConsistentWhitespaceCheckInnerBrace      = $true
        UseConsistentWhitespaceCheckOpenBrace       = $true
        UseConsistentWhitespaceCheckOpenParen       = $true
        UseConsistentWhitespaceCheckOperator        = $true
        UseConsistentWhitespaceCheckPipe            = $true
        UseConsistentWhitespaceCheckSeparator       = $true

        AlignAssignmentStatementEnable              = $true
        AlignAssignmentStatementCheckHashtable      = $true

        UseCorrectCasingEnable                      = $true
    }
    # format PSD1 and PSM1 files when merging into a single file
    # enable formatting is not required as Configuration is provided
    New-ConfigurationFormat -ApplyTo 'OnMergePSM1', 'OnMergePSD1' -Sort None @ConfigurationFormat
    # format PSD1 and PSM1 files within the module
    # enable formatting is required to make sure that formatting is applied (with default settings)
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'DefaultPSM1' -EnableFormatting -Sort None
    # when creating PSD1 use special style without comments and with only required parameters
    New-ConfigurationFormat -ApplyTo 'DefaultPSD1', 'OnMergePSD1' -PSD1Style 'Minimal'

    # configuration for documentation, at the same time it enables documentation processing
    New-ConfigurationDocumentation -Enable -PathReadme 'Docs\Readme.md' -Path 'Docs' -SyncExternalHelpToProjectRoot

    New-ConfigurationImportModule -ImportSelf #-ImportRequiredModules

    $newConfigurationBuildSplat = @{
        Enable                            = $true
        SignModule                        = $SignModule
        MergeModuleOnBuild                = $true
        MergeFunctionsFromApprovedModules = $true
        CertificateThumbprint             = '92E95FB58EFFA6A4A75E77A33CDD6BFE6DD30F1A'
        ResolveBinaryConflicts            = $true
        ResolveBinaryConflictsName        = 'Mailozaurr.PowerShell'
        NETProjectPath                    = 'Sources\Mailozaurr.PowerShell\Mailozaurr.PowerShell.csproj'
        NETProjectName                    = 'Mailozaurr.PowerShell'
        NETConfiguration                  = 'Release'
        NETFramework                      = 'net8.0', 'net472'
        NETHandleAssemblyWithSameName     = $true
        NETAssemblyLoadContext            = $true
        NETAssemblyTypeAcceleratorMode    = 'AllowList'
        NETAssemblyTypeAccelerators       = @(
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
        #NETMergeLibraryDebugging          = $true
        DotSourceLibraries                = $true
        DotSourceClasses                  = $true
        DeleteTargetModuleBeforeBuild     = $true
        NETBinaryModuleDocumentation      = $true
    }

    New-ConfigurationBuild @newConfigurationBuildSplat #-DotSourceLibraries -DotSourceClasses -MergeModuleOnBuild -Enable -SignModule -DeleteTargetModuleBeforeBuild -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703' -MergeFunctionsFromApprovedModules

    New-ConfigurationProjectBuild -Name 'Mailozaurr' -ConfigPath $ProjectBuildConfigPath -Enabled -BuildBeforeModule -UseAsReleaseVersionSource -ProvideLocalNuGetFeed -PublishNuget -PublishGitHub
    New-ConfigurationRelease -StageRoot 'Artefacts\UploadReady' -VersionSource ProjectBuild -PrimaryProject 'Mailozaurr' -BuildOrder 'Packages', 'Module' -PublishOrder 'NuGet', 'PowerShellGallery', 'GitHub'

    New-ConfigurationArtefact -Type Unpacked -Enable -Path 'Artefacts\Unpacked' -ModulesPath 'Artefacts\Unpacked\Modules'
    New-ConfigurationArtefact -Type Packed -Enable -Path 'Artefacts\Packed' -ModulesPath 'Artefacts\Packed\Modules' -IncludeTagName

    #New-ConfigurationTest -TestsPath "$PSScriptRoot\..\Tests" -Enable

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false -UseAsDependencyVersionSource
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -Enabled:$false -RepositoryName 'Mailozaurr' -GenerateReleaseNotes -OverwriteTagName '{ModuleName}-v{ModuleVersionWithPreRelease}'

    New-ConfigurationGate -Mode $ConfigurationGateMode
} -ExitCode
