Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = Get-Credential

$common = @{
    From = 'sender@example.com'
    Server = 'smtp.example.com'
    Credential = $Credential
    UseConnectionPool = $true
    ConnectionPoolSize = 2
    WhatIf = $true
}

# Send a plain text message
Send-EmailMessage @common -To 'user1@example.com' -Subject 'First' -Body 'First message'

# Send a high priority message with an attachment
Send-EmailMessage @common -To 'user2@example.com' -Subject 'Second' -Body 'Second message' -Priority High -Attachment "$PSScriptRoot\..\README.MD"

# Send HTML content
Send-EmailMessage @common -To 'user3@example.com' -Subject 'Third' -HTML '<b>Third message</b>'

# When done with the pool
Clear-SmtpConnectionPool
