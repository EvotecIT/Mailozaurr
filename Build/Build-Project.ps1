param(
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $ConfigurationGateMode = 'Build',

    [string] $ConfigPath = 'Build\project.build.json',

    [Nullable[bool]] $UpdateVersions,

    [Nullable[bool]] $Build,

    [Nullable[bool]] $PublishNuget,

    [Nullable[bool]] $PublishGitHub,

    [Nullable[bool]] $Plan,

    [string] $PlanPath
)

Import-Module PSPublishModule -Force -ErrorAction Stop

$invokeParams = @{
    ConfigPath     = $ConfigPath
    UpdateVersions = @{
        Manifest = $false
        Build    = $true
        Publish  = $true
    }[$ConfigurationGateMode]
    Build          = @{
        Manifest = $false
        Build    = $true
        Publish  = $true
    }[$ConfigurationGateMode]
    PublishNuget   = @{
        Manifest = $false
        Build    = $false
        Publish  = $true
    }[$ConfigurationGateMode]
    PublishGitHub  = @{
        Manifest = $false
        Build    = $false
        Publish  = $true
    }[$ConfigurationGateMode]
}

if ($null -ne $UpdateVersions) { $invokeParams.UpdateVersions = $UpdateVersions }
if ($null -ne $Build) { $invokeParams.Build = $Build }
if ($null -ne $PublishNuget) { $invokeParams.PublishNuget = $PublishNuget }
if ($null -ne $PublishGitHub) { $invokeParams.PublishGitHub = $PublishGitHub }
if ($null -ne $Plan) { $invokeParams.Plan = $Plan }
if ($PlanPath) { $invokeParams.PlanPath = $PlanPath }

Invoke-ProjectBuild @invokeParams
