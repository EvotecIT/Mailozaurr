$GitHubAccessToken = Get-Content -Raw 'C:\Support\Important\GithubAPI.txt'

$publishGitHubReleaseAssetSplat = @{
    ProjectPath          = "$PSScriptRoot\..\Sources\Mailozaurr"
    GitHubAccessToken    = $GitHubAccessToken
    GitHubUsername       = "EvotecIT"
    GitHubRepositoryName = "Mailozaurr"
    IsPreRelease         = $false
}

Publish-GitHubReleaseAsset @publishGitHubReleaseAssetSplat
