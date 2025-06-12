Clear-Host

$ProjectPath = "C:\Support\GitHub\Mailozaurr\Sources\Mailozaurr"
$ReleasePath = "C:\Support\GitHub\Mailozaurr\Sources\Mailozaurr\bin\Release"

[xml] $XML = Get-Content -Raw "C:\Support\GitHub\Mailozaurr\Sources\Mailozaurr\Mailozaurr.csproj"
$Version = $XML.Project.PropertyGroup.VersionPrefix

$ZipPath = "C:\Support\GitHub\Mailozaurr\Sources\Mailozaurr\bin\Release\Mailozaurr.$Version.zip"

Set-Location -LiteralPath $ProjectPath

if (Test-Path -LiteralPath $ReleasePath) {
    $File = Get-ChildItem -Path $ReleasePath -Recurse -File
    foreach ($F in $File) {
        Remove-Item -Path $F.FullName -Force
    }

    $File = Get-ChildItem -Path $ReleasePath -Recurse -Filter "*.nupkg"
    foreach ($F in $File) {
        Remove-Item -Path $F.FullName -Force
    }

    $Folders = Get-ChildItem -Path $ReleasePath -Directory
    foreach ($F in $Folders) {
        Remove-Item -Path $F.FullName -Force -Recurse
    }
}

dotnet build --configuration Release

Register-Certificate -Path $ReleasePath -LocalStore CurrentUser -Include @('*.dll') -TimeStampServer 'http://timestamp.digicert.com' -Thumbprint '483292C9E317AA13B07BB7A96AE9D1A5ED9E7703'
Compress-Archive -Path "$ReleasePath\*" -DestinationPath $ZipPath -Force

dotnet pack --configuration Release --no-restore --no-build

$Nugets = Get-ChildItem -Path $ReleasePath -Recurse -Filter "*.nupkg"
foreach ($Nuget in $Nugets) {
    dotnet nuget sign $Nuget.FullName --certificate-fingerprint "483292C9E317AA13B07BB7A96AE9D1A5ED9E7703" --timestamper 'http://timestamp.digicert.com' --overwrite
}

