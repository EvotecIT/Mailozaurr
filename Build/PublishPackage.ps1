$ProjectPath = "C:\Support\GitHub\Mailozaurr\Sources\Mailozaurr\bin\Release"
$NugetAPI = Get-Content -Raw -LiteralPath "C:\Support\Important\NugetOrg.txt"
$NugetAPI = Get-Content -Raw -LiteralPath "C:\Support\Important\NugetOrgEvotec.txt"
$GitHubAPI = Get-Content -Raw -LiteralPath "C:\Support\Important\GithubAPI.txt"
$File = Get-ChildItem -Path $ProjectPath -Recurse -Filter "*.nupkg"

# publish to nuget.org
if ($File.Count -eq 1) {
    dotnet nuget push $File.FullName --api-key $NugetAPI --source https://api.nuget.org/v3/index.json

    #dotnet nuget add source --username evotecit --password $GitHubAPI --store-password-in-clear-text --name github "https://nuget.pkg.github.com/OWNER/index.json"
    #dotnet nuget push $File.FullName --api-key $GitHubAPI --source "github"
}