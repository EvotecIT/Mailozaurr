$dllPath = Join-Path $PSScriptRoot 'Sources/Mailozaurr.PowerShell/bin/Debug/net8.0/Mailozaurr.PowerShell.dll'
if (-not (Test-Path $dllPath)) {
    Write-Error "Compiled module not found at $dllPath. Build the project before running tests." -ErrorAction Stop
}
& "$PSScriptRoot/Mailozaurr.Tests.ps1"
