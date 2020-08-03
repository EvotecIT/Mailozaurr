function Send-GraphMailMessage {
    [cmdletBinding(SupportsShouldProcess)]
    param(
        [object] $From,
        [Array] $To,
        [Array] $Cc,
        [Array] $Bcc,
        [string] $Subject,
        [alias('Body')][string[]] $HTML,
        [string[]] $Text,
        [alias('Attachments')][string[]] $Attachment,
        [PSCredential] $Credential
    )
    $Authorization = Connect-O365Graph -ApplicationID $ClientID -ApplicationKey $ClientSecret -TenantDomain $DirectoryID -Resource https://graph.microsoft.com
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
        message = @{
            subject         = $Subject
            body            = $Body
            toRecipients    = @(
                ConvertTo-GraphAddress -MailboxAddress $To
            )
            ccRecipients    = @(
                ConvertTo-GraphAddress -MailboxAddress $CC
            )
            bccRecipients   = @(
                ConvertTo-GraphAddress -MailboxAddress $BCC
            )
            attachments     = @(
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
            saveToSentItems = $true
        }
    }
    $MailSentTo = -join ($To -join ',', $CC -join ', ', $Bcc -join ', ')

    $Body = $Message | ConvertTo-Json -Depth 5
    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $null = Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users/$From/sendMail" -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json' -ErrorAction Stop
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status = $True
                    Error  = ''
                    SentTo = $MailSentTo
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
                Status = $False
                Error  = $($_.Exception.Message)
                SentTo = $MailSentTo
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