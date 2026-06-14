$pending = Join-Path $PSScriptRoot 'pending'
Send-EmailPendingMessage -PendingMessagesPath $pending -WhatIf
