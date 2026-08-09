if ($env:MAILOZAURR_TEST_MODULE_PATH) {
    Remove-Item Env:MAILOZAURR_DEVELOPMENT -ErrorAction SilentlyContinue
    Import-Module $env:MAILOZAURR_TEST_MODULE_PATH -Force
} else {
    $env:MAILOZAURR_DEVELOPMENT = '1'
    Import-Module (Resolve-Path "$PSScriptRoot/../Mailozaurr.psd1") -Force
}

Describe 'Send-EmailMessage - MgGraphRequest attachments' {
    It 'Uploads large attachments to the draft before sending' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method      = $Method
                    Uri         = $Uri
                    ContentType = $ContentType
                    Body        = $Body
                    Headers     = $Headers
                })

            if ($Method -eq 'POST' -and $Uri -like '*/mailfolders/drafts/messages') {
                return @{ id = 'draft-id' }
            }
            if ($Method -eq 'POST' -and $Uri -like '*/attachments/createUploadSession') {
                return @{ uploadUrl = 'https://upload.example/session' }
            }
            if ($Method -eq 'POST' -and $Uri -like '*/messages/draft-id/send') {
                return @{}
            }
        }

        $file = Join-Path $TestDrive 'large.bin'
        [System.IO.File]::WriteAllBytes($file, (New-Object byte[] 4000001))

        try {
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Subject 's' -Body 'b' -MgGraphRequest -Attachment $file -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $script:mgGraphRequestCalls.Count | Should -Be 4
        $script:mgGraphRequestCalls[0].Uri | Should -Be 'v1.0/users/from@example.com/mailfolders/drafts/messages'
        $script:mgGraphRequestCalls[1].Uri | Should -Be "https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/attachments/createUploadSession"
        $script:mgGraphRequestCalls[2].Method | Should -Be 'PUT'
        $script:mgGraphRequestCalls[2].Uri | Should -Be 'https://upload.example/session'
        $script:mgGraphRequestCalls[2].Headers['Content-Range'].ToString() | Should -Match '^bytes 0-'
        $script:mgGraphRequestCalls[3].Uri | Should -Be "https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/send"
    }

    It 'adds a sub-3MB file directly when the complete request requires a draft' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method = $Method
                    Uri    = $Uri
                    Body   = $Body
                })

            if ($Uri -like '*/mailfolders/drafts/messages') {
                return @{ id = 'draft-id' }
            }
            return @{}
        }

        $file = Join-Path $TestDrive 'small.bin'
        [System.IO.File]::WriteAllBytes($file, (New-Object byte[] 2300000))

        try {
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Subject 's' -Body ('x' * 1000000) -MgGraphRequest -Attachment $file -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $script:mgGraphRequestCalls.Count | Should -Be 3
        $script:mgGraphRequestCalls.Uri | Should -Not -Contain "https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/attachments/createUploadSession"
        $script:mgGraphRequestCalls[0].Body | Should -Not -Match '"attachments"'
        $script:mgGraphRequestCalls[1].Uri | Should -Be "https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/attachments"
        $script:mgGraphRequestCalls[1].Body | Should -Match '"contentBytes"'
        $script:mgGraphRequestCalls[2].Uri | Should -Be "https://graph.microsoft.com/v1.0/users('from@example.com')/messages/draft-id/send"
    }

    It 'maps PSCustomObject text-only content to a Graph text body' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method = $Method
                    Uri    = $Uri
                    Body   = $Body
                })
            return @{}
        }

        try {
            $content = [pscustomobject] @{
                Subject   = 'Text report'
                PlainText = 'Only the plain-text alternative'
                Headers   = [pscustomobject] @{ 'X-Workflow' = 'text-report' }
            }
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -MgGraphRequest -Content $content -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $script:mgGraphRequestCalls.Count | Should -Be 1
        $message = $script:mgGraphRequestCalls[0].Body | ConvertFrom-Json
        $message.message.subject | Should -Be 'Text report'
        $message.message.body.contentType | Should -Be 'Text'
        $message.message.body.content | Should -Be 'Only the plain-text alternative'
        $message.message.internetMessageHeaders.name | Should -Contain 'X-Workflow'
    }

    It 'maps generic key-value header collections without exposing collection properties' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method = $Method
                    Uri    = $Uri
                    Body   = $Body
                })
            return @{}
        }

        try {
            $headers = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, string]]]::new()
            $headers.Add([System.Collections.Generic.KeyValuePair[string, string]]::new('X-Workflow', 'generic-list'))
            $content = [pscustomobject] @{
                Subject   = 'Generic headers'
                PlainText = 'body'
                Headers   = [System.Management.Automation.PSObject]::AsPSObject($headers)
            }
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -MgGraphRequest -Content $content -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $message = $script:mgGraphRequestCalls[0].Body | ConvertFrom-Json
        $header = $message.message.internetMessageHeaders | Where-Object name -eq 'X-Workflow'
        $header.value | Should -Be 'generic-list'
        $message.message.internetMessageHeaders.name | Should -Not -Contain 'Count'
    }

    It 'keeps explicitly bound headers ahead of generic content headers' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method = $Method
                    Uri    = $Uri
                    Body   = $Body
                })
            return @{}
        }

        try {
            $headers = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string, string]]]::new()
            $headers.Add([System.Collections.Generic.KeyValuePair[string, string]]::new('X-Workflow', 'content'))
            $content = [pscustomobject] @{
                Subject   = 'Header override'
                PlainText = 'body'
                Headers   = $headers
            }
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -MgGraphRequest -Content $content -Headers @{ 'X-Workflow' = 'explicit' } -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $message = $script:mgGraphRequestCalls[0].Body | ConvertFrom-Json
        $header = $message.message.internetMessageHeaders | Where-Object name -eq 'X-Workflow'
        $header.value | Should -Be 'explicit'
    }

    It 'maps a singleton PSCustomObject attachment from transport-neutral content' {
        $script:mgGraphRequestCalls = [System.Collections.Generic.List[object]]::new()

        function global:Invoke-MgGraphRequest {
            param(
                [string] $Method,
                [string] $Uri,
                [string] $ContentType,
                [object] $Body,
                [hashtable] $Headers
            )

            $script:mgGraphRequestCalls.Add([pscustomobject] @{
                    Method = $Method
                    Uri    = $Uri
                    Body   = $Body
                })
            return @{}
        }

        try {
            $content = [pscustomobject] @{
                Subject     = 'Attachment report'
                Html        = '<p>Attached</p>'
                Attachments = [pscustomobject] @{
                    Data     = [byte[]] (1, 2, 3)
                    FileName = 'report.bin'
                    MimeType = 'application/x-report'
                }
            }
            Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -MgGraphRequest -Content $content -Confirm:$false | Out-Null
        } finally {
            Remove-Item -Path function:global:Invoke-MgGraphRequest -ErrorAction SilentlyContinue
        }

        $message = $script:mgGraphRequestCalls[0].Body | ConvertFrom-Json
        $message.message.attachments.name | Should -Be 'report.bin'
        $message.message.attachments.contentType | Should -Be 'application/x-report'
        $message.message.attachments.contentBytes | Should -Be 'AQID'
    }
}
