# Install-Module PSPublishModule -Force
Import-Module PSPublishModule -Force

Build-Module -ModuleName 'Mailozaurr' {
    # Usual defaults as per standard module
    $Manifest = [ordered] @{
        ModuleVersion        = '2.0.0'
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
        Description          = 'Mailozaurr is a PowerShell module that aims to provide SMTP, POP3, IMAP and few other ways to interact with Email. Underneath it uses MimeKit and MailKit and EmailValidation libraries written by Jeffrey Stedfast.            '
        # Minimum version of the Windows PowerShell engine required by this module
        PowerShellVersion    = '5.1'
        # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
        Tags                 = @('Windows', 'MacOS', 'Linux', 'Mail', 'Email', 'MX', 'SPF', 'DMARC', 'DKIM', 'GraphApi', 'SendGrid', 'Graph', 'IMAP', 'POP3')

        IconUri              = 'https://evotec.xyz/wp-content/uploads/2020/07/MailoZaurr.png'

        ProjectUri           = 'https://github.com/EvotecIT/MailoZaurr'

        PreReleaseTag        = 'Preview3'
    }
    New-ConfigurationManifest @Manifest
    # Add standard module dependencies (directly, but can be used with loop as well)
    New-ConfigurationModule -Type RequiredModule -Name 'PSSharedGoods' -Guid 'Auto' -Version 'Latest'
    # Add external module dependencies, using loop for simplicity
    New-ConfigurationModule -Type ExternalModule -Name 'Microsoft.PowerShell.Utility', 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Security'
    # Add approved modules, that can be used as a dependency, but only when specific function from those modules is used
    # And on that time only that function and dependant functions will be copied over
    # Keep in mind it has it's limits when "copying" functions such as it should not depend on DLLs or other external files
    New-ConfigurationModule -Type ApprovedModule -Name 'PSSharedGoods', 'PSWriteColor', 'Connectimo', 'PSUnifi', 'PSWebToolbox', 'PSMyPassword'

    New-ConfigurationModuleSkip -IgnoreModuleName 'Microsoft.Graph.Authentication' -IgnoreFunc @(
        'Resolve-DnsName'
    )

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
    New-ConfigurationDocumentation -Enable:$false -StartClean -UpdateWhenNew -PathReadme 'Docs\Readme.md' -Path 'Docs'

    New-ConfigurationImportModule -ImportSelf #-ImportRequiredModules


    $newConfigurationBuildSplat = @{
        Enable                            = $true
        SignModule                        = $true
        MergeModuleOnBuild                = $true
        MergeFunctionsFromApprovedModules = $true
        CertificateThumbprint             = '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'
        ResolveBinaryConflicts            = $true
        ResolveBinaryConflictsName        = 'Mailozaurr'
        NETProjectName                    = 'Mailozaurr'
        NETConfiguration                  = 'Release'
        NETFramework                      = 'netstandard2.0', 'net472'
        NETHandleAssemblyWithSameName     = $true
        #NETMergeLibraryDebugging          = $true
        DotSourceLibraries                = $true
        DotSourceClasses                  = $true
        DeleteTargetModuleBeforeBuild     = $true
    }

    New-ConfigurationBuild @newConfigurationBuildSplat #-DotSourceLibraries -DotSourceClasses -MergeModuleOnBuild -Enable -SignModule -DeleteTargetModuleBeforeBuild -CertificateThumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703' -MergeFunctionsFromApprovedModules

    New-ConfigurationArtefact -Type Unpacked -Enable -Path "$PSScriptRoot\..\Artefacts" -RequiredModulesPath "$PSScriptRoot\..\Artefacts\Modules"
    New-ConfigurationArtefact -Type Packed -Enable -Path "$PSScriptRoot\..\Releases" -IncludeTagName

    #New-ConfigurationTest -TestsPath "$PSScriptRoot\..\Tests" -Enable

    # global options for publishing to github/psgallery
    #New-ConfigurationPublish -Type PowerShellGallery -FilePath 'C:\Support\Important\PowerShellGalleryAPI.txt' -Enabled:$true
    #New-ConfigurationPublish -Type GitHub -FilePath 'C:\Support\Important\GitHubAPI.txt' -UserName 'EvotecIT' -Enabled:$true
} -ExitCode