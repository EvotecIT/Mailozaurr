[CmdletBinding()]
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\Sources\Mailozaurr\Resources')
)

$lists = @{
    'disposable_email_blocklist.conf' = 'https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/refs/heads/main/disposable_email_blocklist.conf'
    'allowlist.conf'                 = 'https://raw.githubusercontent.com/disposable-email-domains/disposable-email-domains/refs/heads/main/allowlist.conf'
}

foreach ($list in $lists.GetEnumerator()) {
    $target = Join-Path $OutputPath $list.Key
    Write-Host "Checking $($list.Key)..."
    $remoteContent = (Invoke-WebRequest -Uri $list.Value -UseBasicParsing).Content -replace "`r`n", "`n" -split "`n" | Where-Object { $_ -and -not $_.StartsWith('#') }
    if (Test-Path $target) {
        $localContent = Get-Content $target
        $diff = Compare-Object -ReferenceObject $localContent -DifferenceObject $remoteContent
        if ($diff) {
            Write-Host "Changes for $($list.Key):"
            foreach ($d in $diff) {
                if ($d.SideIndicator -eq '=>') { Write-Host "Added: $($d.InputObject)" }
                elseif ($d.SideIndicator -eq '<=') { Write-Host "Removed: $($d.InputObject)" }
            }
        } else {
            Write-Host "No changes for $($list.Key)."
        }
    } else {
        Write-Host "Local file for $($list.Key) not found. Creating new."
        foreach ($line in $remoteContent) { Write-Host "Added: $line" }
    }
    $remoteContent | Set-Content $target -Encoding UTF8
}
