Import-Module PSPublishModule -Force -ErrorAction Stop

$GitHubAccessToken = Get-Content -Raw 'C:\Support\Important\GithubAPI.txt'

$publishGitHubReleaseAssetSplat = @{
    ProjectPath          = @(
        "$PSScriptRoot\..\Sources\Mailozaurr"
        "$PSScriptRoot\..\Sources\Mailozaurr.Msg"
    )
    GitHubAccessToken    = $GitHubAccessToken
    GitHubUsername       = "EvotecIT"
    GitHubRepositoryName = "Mailozaurr"
    IsPreRelease         = $false
    GenerateReleaseNotes = $true
}

Publish-GitHubReleaseAsset @publishGitHubReleaseAssetSplat
