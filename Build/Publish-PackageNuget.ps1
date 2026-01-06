Import-Module PSPublishModule -Force -ErrorAction Stop

$NugetAPI = Get-Content -Raw -LiteralPath "C:\Support\Important\NugetOrgEvotec.txt"

Publish-NugetPackage -Path @(
    "$PSScriptRoot\..\Sources\Mailozaurr\bin\Release"
    "$PSScriptRoot\..\Sources\Mailozaurr.Msg\bin\Release"
) -ApiKey $NugetAPI -SkipDuplicate
