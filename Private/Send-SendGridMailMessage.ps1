function Send-SendGridMailMessage {
    [CmdletBinding(SupportsShouldProcess)]
    param (
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
    # https://sendgrid.api-docs.io/v3.0/mail-send/v3-mail-send
    if ($Credential) {
        $AuthorizationData = ConvertFrom-OAuth2Credential -Credential $Credential
    } else {
        return
    }
    $SendGridMessage = [ordered]@{
        personalizations = [System.Collections.Generic.List[object]]::new()
        from             = @{email = $from }
        content          = @(
            @{
                type  = if ($HTML) { 'text/html' } else { 'text/plain' }
                value = if ($HTML) { $HTML } else { $Text }
            }
        )
    }
    $Personalization = [ordered] @{
        to      = foreach ($T in $To) {
            @{email = $T }
        }
        subject = $Subject
    }

    $SendGridMessage.personalizations.Add($Personalization)
    $InvokeRestMethodParams = [ordered] @{
        URI         = 'https://api.sendgrid.com/v3/mail/send'
        Headers     = @{'Authorization' = "Bearer $($AuthorizationData.Token)" }
        Method      = 'POST'
        Body        = $SendGridMessage | ConvertTo-Json -Depth 5
        ErrorAction = 'Stop'
        ContentType = 'application/json; charset=utf-8'
    }

    try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $null = Invoke-RestMethod @InvokeRestMethodParams #-Verbose:$false
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
        } else {
            Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message)"
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
    # This is to make sure data doesn't flood with attachments content
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
        $TrimmedBody = $SendGridMessage | ConvertTo-Json -Depth 5
        Write-Verbose "Message content: $TrimmedBody"
    }
}