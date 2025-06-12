[xml] $XML = Get-Content -Raw "C:\Support\GitHub\DnsClientX\DnsClientX\DnsClientX.csproj"
$Version = $XML.Project.PropertyGroup.VersionPrefix
$ZipPath = "C:\Support\GitHub\DnsClientX\DnsClientX\bin\Release\DnsClientX.$Version.zip"
$IsPreRelease = $false
$TagName = "v$Version"
$GitHubAccessToken = Get-Content -Raw 'C:\Support\Important\GithubAPI.txt'
$UserName = 'EvotecIT'
$GitHubRepositoryName = 'DnsClientX'

if (Test-Path -LiteralPath $ZipPath) {
    $StatusGithub = Send-GitHubRelease -GitHubUsername $UserName -GitHubRepositoryName $GitHubRepositoryName -GitHubAccessToken $GitHubAccessToken -TagName $TagName -AssetFilePaths $ZipPath -IsPreRelease $IsPreRelease
    $StatusGithub
}