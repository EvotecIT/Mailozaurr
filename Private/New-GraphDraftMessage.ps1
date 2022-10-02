function New-GraphDraftMessage {
    [cmdletbinding(SupportsShouldProcess)]
    param(
        [string] $MailSentTo,
        [string] $FromField,
        [System.Collections.IDictionary] $Authorization,
        [string] $Body
    )

    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $Uri = "https://graph.microsoft.com/v1.0/users/$FromField/mailfolders/drafts/messages"
            #$Authorization['AnchorMailbox'] = $FromField

            #$Uri = "https://graph.microsoft.com/v1.0/users/$FromField/sendMail"
            $OutputRest = Invoke-RestMethod -Uri $Uri -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            $OutputRest
            # if (-not $Suppress) {
            #     [PSCustomObject] @{
            #         Status        = $True
            #         Error         = ''
            #         SentTo        = $MailSentTo
            #         SentFrom      = $FromField
            #         Message       = ''
            #         TimeToExecute = $StopWatch.Elapsed
            #     }
            # }
        } else {
            # if (-not $Suppress) {
            #     [PSCustomObject] @{
            #         Status        = $false
            #         Error         = 'Email not sent (WhatIf)'
            #         SentTo        = $MailSentTo
            #         SentFrom      = $FromField
            #         Message       = ''
            #         TimeToExecute = $StopWatch.Elapsed
            #     }
            # }
        }
    } catch {
        if ($PSBoundParameters.ErrorAction -eq 'Stop') {
            throw
            return
        }
        $RestError = $_.ErrorDetails.Message
        $RestMessage = $_.Exception.Message
        if ($RestError) {
            try {
                $ErrorMessage = ConvertFrom-Json -InputObject $RestError -ErrorAction Stop
                $ErrorText = $ErrorMessage.error.message
                Write-Warning -Message "Send-GraphMailMessage - Error during draft message creation: $($RestMessage) $($ErrorText)"
            } catch {
                $ErrorText = ''
                Write-Warning -Message "Send-GraphMailMessage - Error during draft message creation: $($RestMessage)"
            }
        } else {
            Write-Warning -Message "Send-GraphMailMessage - Error during draft message creation: $($_.Exception.Message)"
        }
        if ($_.ErrorDetails.RecommendedAction) {
            Write-Warning -Message "Send-GraphMailMessage - Error during draft message creation. Recommended action: $RecommendedAction"
        }
        # if (-not $Suppress) {
        #     [PSCustomObject] @{
        #         Status        = $False
        #         Error         = if ($RestError) { "$($RestMessage) $($ErrorText)" }  else { $RestMessage }
        #         SentTo        = $MailSentTo
        #         SentFrom      = $FromField
        #         Message       = ''
        #         TimeToExecute = $StopWatch.Elapsed
        #     }
        # }
    }
}