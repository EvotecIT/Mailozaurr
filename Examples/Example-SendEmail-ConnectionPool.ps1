Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# Obtain credentials for SMTP authentication
$Credential = Get-Credential

$common = @{From='sender@example.com'; To='recipient@example.com'; Subject='Test'; Server='smtp.example.com'; Credential=$Credential; WhatIf=$true}

# First send creates and pools the connection
Send-EmailMessage @common -UseConnectionPool -ConnectionPoolSize 3

# Second send reuses the connection from the pool
Send-EmailMessage @common -UseConnectionPool
