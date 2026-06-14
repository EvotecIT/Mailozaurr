Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Display pool size changes using a helper cmdlet
$watcher = Watch-SmtpConnectionPool -Action { param($s) Write-Host "Pool size: $($s.CurrentPoolSize)" }

# Reset any existing pooled connections
Clear-SmtpConnectionPool

$common = @{
    From = 'sender@example.com'
    To = 'recipient@example.com'
    Subject = 'Test'
    Server = 'smtp.example.com'
    UseConnectionPool = $true
    ConnectionPoolSize = 3
    WhatIf = $true
}

# Send twice to show pool size events
Send-EmailMessage @common
Send-EmailMessage @common

# Clear the pool and display final size
Clear-SmtpConnectionPool
$snapshot = Get-SmtpConnectionPool
Write-Host "Current pool size: $($snapshot.CurrentPoolSize)"

Unregister-SmtpConnectionPoolWatcher -Watcher $watcher
