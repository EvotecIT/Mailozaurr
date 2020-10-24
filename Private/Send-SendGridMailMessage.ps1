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
        [switch] $SeparateTo
    )
    # https://sendgrid.api-docs.io/v3.0/mail-send/v3-mail-send
    if ($Credential) {
        $AuthorizationData = ConvertFrom-OAuth2Credential -Credential $Credential
    } else {
        return
    }
    $SendGridMessage = [ordered]@{
        personalizations = [System.Collections.Generic.List[object]]::new()
        from             = ConvertTo-SendGridAddress -From $From
        content          = @(
            @{
                type  = if ($HTML) { 'text/html' } else { 'text/plain' }
                value = if ($HTML) { $HTML } else { $Text }
            }
        )
    }

    if ($ReplyTo) {
        $SendGridMessage["reply_to"] = ConvertTo-SendGridAddress -ReplyTo $ReplyTo
    }
    if ($Subject.Length -le 1) {
        # Subject must be at least char in lenght
        $Subject = ' '
    }

    [Array] $SendGridTo = ConvertTo-SendGridAddress -MailboxAddress $To
    [Array] $SendGridCC = ConvertTo-SendGridAddress -MailboxAddress $CC
    [Array] $SendGridBCC = ConvertTo-SendGridAddress -MailboxAddress $Bcc

    if ($SeparateTo) {
        if ($CC -or $BCC) {
            Write-Warning "Send-EmailMessage - Using SeparateTo parameter where there are multiple recipients for TO and CC or BCC is not supported by SendGrid."
            Write-Warning "Send-EmailMessage - SendGrid requires unique email addresses to be available as part of all recipient fields."
            Write-Warning "Send-EmailMessage - Please use SeparateTo parameter only with TO field. Skipping CC/BCC."
        }
        foreach ($T in $To) {
            $Personalization = @{
                subject = $Subject
                to      = @(
                    ConvertTo-SendGridAddress -MailboxAddress $T
                )
            }
            Remove-EmptyValue -Hashtable $Personalization -Recursive
            $SendGridMessage.personalizations.Add($Personalization)
        }
    } else {
        $Personalization = [ordered] @{
            cc      = $SendGridCC
            bcc     = $SendGridBCC
            to      = $SendGridTo
            subject = $Subject
        }
        Remove-EmptyValue -Hashtable $Personalization -Recursive
        $SendGridMessage.personalizations.Add($Personalization)
    }

    Remove-EmptyValue -Hashtable $SendGridMessage -Recursive -Rerun 2

    $InvokeRestMethodParams = [ordered] @{
        URI         = 'https://api.sendgrid.com/v3/mail/send'
        Headers     = @{'Authorization' = "Bearer $($AuthorizationData.Token)" }
        Method      = 'POST'
        Body        = $SendGridMessage | ConvertTo-Json -Depth 5
        ErrorAction = 'Stop'
        ContentType = 'application/json; charset=utf-8'
    }

    [Array] $MailSentTo = ($SendGridTo.Email, $SendGridCC.Email, $SendGridBCC.Email) | ForEach-Object { if ($_) { $_ } }
    [string] $MailSentList = $MailSentTo -join ','
    try {
        if ($PSCmdlet.ShouldProcess("$MailSentList", 'Send-EmailMessage')) {
            $null = Invoke-RestMethod @InvokeRestMethodParams
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status   = $True
                    Error    = ''
                    SentTo   = $MailSentList
                    SentFrom = $SendGridMessage.From.Email
                }
            }
        }
    } catch {
        # This tries to help user with some assesment
        if ($MailSentTo.Count -gt ($MailSentTo | Sort-Object -Unique).Count) {
            $ErrorDetails = ' Addresses in TO/CC/BCC fields must be unique across all fields which may be reason for a failure.'
        } else {
            $ErrorDetails = ''
        }
        # And here we process error
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            Write-Error $_
        } else {
            Write-Warning "Send-EmailMessage - Error: $($_.Exception.Message) $ErrorDetails"
        }
        if (-not $Suppress) {
            [PSCustomObject] @{
                Status   = $False
                Error    = -join ( $($_.Exception.Message), $ErrorDetails)
                SentTo   = $MailSentTo
                SentFrom = $SendGridMessage.From.Email
            }
        }
    }
    # This is to make sure data doesn't flood with attachments content
    if ($VerbosePreference) {
        <#
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
        #>
        $TrimmedBody = $SendGridMessage | ConvertTo-Json -Depth 5
        Write-Verbose "Message content: $TrimmedBody"
    }
}