Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Examine server capabilities and check if connections stay open
$info = Test-SmtpConnection -Server 'smtp.example.com' -Port 587

if ($info.Persistent) {
    Write-Host "Server keeps the connection open. Enabling pooling."
    $poolSettings = @{ UseConnectionPool = $true; ConnectionPoolSize = 2 }
} else {
    Write-Warning "Server closes the connection after each command. Pooling disabled."
    $poolSettings = @{}
}

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Server 'smtp.example.com' @poolSettings -WhatIf
