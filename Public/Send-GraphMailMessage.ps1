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
        [alias('Importance')][ValidateSet('Low', 'Normal', 'High')][string] $Priority
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
            subject                = $Subject
            body                   = $Body
            from                   = ConvertTo-GraphAddress -From $ReplyTo
            toRecipients           = @(
                ConvertTo-GraphAddress -MailboxAddress $To
            )
            ccRecipients           = @(
                ConvertTo-GraphAddress -MailboxAddress $CC
            )
            bccRecipients          = @(
                ConvertTo-GraphAddress -MailboxAddress $BCC
            )
            #sender                 = @(
            #    ConvertTo-GraphAddress -MailboxAddress $From
            #)
            replyTo                = @(
                ConvertTo-GraphAddress -MailboxAddress $ReplyTo
            )
            attachments            = @(
                foreach ($A in $Attachment) {
                    $ItemInformation = Get-Item -Path $FilePath
                    if ($ItemInformation) {
                        $File = [system.io.file]::ReadAllBytes($FilePath)
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
            importance             = $Priority
            isReadReceiptRequested = $true
        }
        saveToSentItems = $true
    }
    $MailSentTo = -join ($To -join ',', $CC -join ', ', $Bcc -join ', ')
    Remove-EmptyValue -Hashtable $Message -Recursive
    $Body = $Message | ConvertTo-Json -Depth 5
    $FromField = ConvertTo-GraphAddress -From $From -LimitedFrom
    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $null = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users/$FromField/sendMail" -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json' -ErrorAction Stop
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status   = $True
                    Error    = ''
                    SentTo   = $MailSentTo
                    SentFrom = $FromField
                }
            }
        }
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
            return
        } else {
            $ErrorDetails = $_.ErrorDetails.Message | ConvertFrom-Json
            Write-Warning "Send-GraphMailMessage - Error: $($_.Exception.Message)"
            Write-Warning "Send-GraphMailMessage - Error details: $($ErrorDetails.Error.Message)"
        }
        if (-not $Suppress) {
            [PSCustomObject] @{
                Status   = $False
                Error    = -join ( $($_.Exception.Message), ' details: ', $($ErrorDetails.Error.Message))
                SentTo   = $MailSentTo
                SentFrom = $FromField
            }
        }
    }
    if ($VerbosePreference) {
        if ($Message.message.attachments) {
            $Message.message.attachments | ForEach-Object {
                $_.contentBytes = -join ($_.contentBytes.Substring(0, 10), 'ContentIsTrimmed')
            }
        }
        If ($Message.message.body.content) {
            $Message.message.body.content = -join ($Message.message.body.content.Substring(0, 10), 'ContentIsTrimmed')
        }
        $TrimmedBody = $Message | ConvertTo-Json -Depth 5
        Write-Verbose "Message content: $TrimmedBody"
    }
}