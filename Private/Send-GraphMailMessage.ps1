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
        [switch] $DoNotSaveToSentItems
    )
    if ($Credential) {
        $AuthorizationData = ConvertFrom-GraphCredential -Credential $Credential
    } else {
        return
    }
    $Authorization = Connect-O365Graph -ApplicationID $AuthorizationData.ClientID -ApplicationKey $AuthorizationData.ClientSecret -TenantDomain $AuthorizationData.DirectoryID -Resource https://graph.microsoft.com
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
            subject       = $Subject
            body          = $Body
            from          = ConvertTo-GraphAddress -From $From
            toRecipients  = @(
                ConvertTo-GraphAddress -MailboxAddress $To
            )
            ccRecipients  = @(
                ConvertTo-GraphAddress -MailboxAddress $CC
            )
            bccRecipients = @(
                ConvertTo-GraphAddress -MailboxAddress $BCC
            )
            #sender                 = @(
            #    ConvertTo-GraphAddress -MailboxAddress $From
            #)
            replyTo       = @(
                ConvertTo-GraphAddress -MailboxAddress $ReplyTo
            )
            attachments   = @(
                foreach ($A in $Attachment) {
                    $ItemInformation = Get-Item -Path $A
                    if ($ItemInformation) {
                        $File = [system.io.file]::ReadAllBytes($A)
                        $Bytes = [System.Convert]::ToBase64String($File)
                        @{
                            '@odata.type'  = '#microsoft.graph.fileAttachment'
                            'name'         = $ItemInformation.Name
                            #'contentType'  = 'text/plain'
                            'contentBytes' = $Bytes
                        }
                    }
                }
            )
            importance    = $Priority
            #isReadReceiptRequested     = $true
            #isDeliveryReceiptRequested = $true
        }
        saveToSentItems = -not $DoNotSaveToSentItems.IsPresent
    }
    $MailSentTo = -join ($To -join ',', $CC -join ', ', $Bcc -join ', ')
    Remove-EmptyValue -Hashtable $Message -Recursive -Rerun 2
    $Body = $Message | ConvertTo-Json -Depth 5
    $FromField = ConvertTo-GraphAddress -From $From -LimitedFrom
    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $null = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users/$FromField/sendMail" -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status   = $True
                    Error    = ''
                    SentTo   = $MailSentTo
                    SentFrom = $FromField
                }
            }
        } else {
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status = $false
                    Error  = 'Email not sent (WhatIf)'
                    SentTo = $MailSentTo
                }
            }
        }
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
        }
        $RestError = $_.ErrorDetails.Message
        $RestMessage = $_.Exception.Message
        if ($RestError) {
            try {
                $ErrorMessage = ConvertFrom-Json -InputObject $RestError -ErrorAction Stop
                $ErrorText = $ErrorMessage.error.message
                # Write-Warning -Message "Invoke-Graph - [$($ErrorMessage.error.code)] $($ErrorMessage.error.message), exception: $($_.Exception.Message)"
                Write-Warning -Message "Send-GraphMailMessage - Error: $($RestMessage) $($ErrorText)"
            } catch {
                $ErrorText = ''
                Write-Warning -Message "Send-GraphMailMessage - Error: $($RestMessage)"
            }
        } else {
            Write-Warning -Message "Send-GraphMailMessage - Error: $($_.Exception.Message)"
        }
        if ($_.ErrorDetails.RecommendedAction) {
            Write-Warning -Message "Send-GraphMailMessage - Recommended action: $RecommendedAction"
        }
        if (-not $Suppress) {
            [PSCustomObject] @{
                Status   = $False
                Error    = if ($RestError) { "$($RestMessage) $($ErrorText)" }  else { $RestMessage }
                SentTo   = $MailSentTo
                SentFrom = $FromField
            }
        }
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