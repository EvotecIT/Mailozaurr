function Send-GraphMailMessage {
    [cmdletBinding(SupportsShouldProcess)]
    param(
        [object] $From,
        [Array] $To,
        [Array] $Cc,
        [Array] $Bcc,
        [string] $ReplyTo,
        [string] $Subject,
        [alias('Body')][string[]] $HTML,
        [string[]] $Text,
        [alias('Attachments')][string[]] $Attachment,
        [PSCredential] $Credential,
        [alias('Importance')][ValidateSet('Low', 'Normal', 'High')][string] $Priority,
        [switch] $DoNotSaveToSentItems,
        [switch] $RequestReadReceipt,
        [switch] $RequestDeliveryReceipt,
        [System.Diagnostics.Stopwatch] $StopWatch,
        [switch] $Suppress
    )
    if ($Credential) {
        $AuthorizationData = ConvertFrom-GraphCredential -Credential $Credential
    } else {
        return
    }
    if ($AuthorizationData.ClientID -eq 'MSAL') {
        $Authorization = Connect-O365GraphMSAL -ApplicationKey $AuthorizationData.ClientSecret
    } else {
        $Authorization = Connect-O365Graph -ApplicationID $AuthorizationData.ClientID -ApplicationKey $AuthorizationData.ClientSecret -TenantDomain $AuthorizationData.DirectoryID -Resource https://graph.microsoft.com
    }
    $Body = @{}
    if ($HTML) {
        $Body['contentType'] = 'HTML'
        $body['content'] = $HTML -join [System.Environment]::NewLine
    } elseif ($Text) {
        $Body['contentType'] = 'Text'
        $body['content'] = $Text -join [System.Environment]::NewLine
    } else {
        $Body['contentType'] = 'Text'
        $body['content'] = ''
    }

    $Message = [ordered] @{
        # https://docs.microsoft.com/en-us/graph/api/resources/message?view=graph-rest-1.0
        message         = [ordered] @{
            subject                    = $Subject
            body                       = $Body
            from                       = ConvertTo-GraphAddress -From $From
            toRecipients               = @(
                ConvertTo-GraphAddress -MailboxAddress $To
            )
            ccRecipients               = @(
                ConvertTo-GraphAddress -MailboxAddress $CC
            )
            bccRecipients              = @(
                ConvertTo-GraphAddress -MailboxAddress $BCC
            )
            #sender                 = @(
            #    ConvertTo-GraphAddress -MailboxAddress $From
            #)
            replyTo                    = @(
                ConvertTo-GraphAddress -MailboxAddress $ReplyTo
            )
            importance                 = $Priority
            isReadReceiptRequested     = $RequestReadReceipt.IsPresent
            isDeliveryReceiptRequested = $RequestDeliveryReceipt.IsPresent
        }
        saveToSentItems = -not $DoNotSaveToSentItems.IsPresent
    }
    $MailSentTo = -join ($To -join ',', $CC -join ', ', $Bcc -join ', ')
    $FromField = ConvertTo-GraphAddress -From $From -LimitedFrom
    Remove-EmptyValue -Hashtable $Message -Recursive -Rerun 2

    if ($Attachment -and (IsLargerThan -FilePath $Attachment -Size 3000000)) {
        $BodyDraft = $Message.Message | ConvertTo-Json -Depth 5
        $DraftMessage = New-GraphDraftMessage -Body $BodyDraft -MailSentTo $MailSentTo -Authorization $Authorization -FromField $FromField
        $null = New-GraphAttachment -DraftMessage $DraftMessage -FromField $FromField -Attachments $Attachment -Authorization $Authorization
        Send-GraphMailMessageDraft -DraftMessage $DraftMessage -Authorization $Authorization -FromField $FromField -StopWatch $StopWatch -Suppress:$Suppress -MailSentTo $MailSentTo
    } else {
        # No attachments or attachments are under 4MB
        if ($Attachment) {
            $Message['message']['attachments'] = @(ConvertTo-GraphAttachment -Attachment $Attachment)
        }
        $Body = $Message | ConvertTo-Json -Depth 5
        New-GraphSendMessage -Body $Body -StopWatch $StopWatch -MailSentTo $MailSentTo -Authorization $Authorization -FromField $FromField -Suppress:$Suppress
    }
    if ($VerbosePreference) {
        if ($Message.message.attachments) {
            $Message.message.attachments | ForEach-Object {
                if ($_.contentBytes.Length -ge 10) {
                    $_.contentBytes = -join ($_.contentBytes.Substring(0, 10), 'ContentIsTrimmed')
                } else {
                    $_.contentBytes = -join ($_.contentBytes, 'ContentIsTrimmed')
                }

            }
        }
        If ($Message.message.body.content) {
            if ($Message.message.body.content.Length -gt 10) {
                $Message.message.body.content = -join ($Message.message.body.content.Substring(0, 10), 'ContentIsTrimmed')
            } else {
                $Message.message.body.content = -join ($Message.message.body.content, 'ContentIsTrimmed')
            }

        }
        $TrimmedBody = $Message | ConvertTo-Json -Depth 5
        Write-Verbose "Message content: $TrimmedBody"
    }
}