function New-GraphDraftMessage {
    [cmdletbinding(SupportsShouldProcess)]
    param(
        [string] $MailSentTo,
        [string] $FromField,
        [System.Collections.IDictionary] $Authorization,
        [string] $Body,
        [switch] $MgGraphRequest
    )

    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $Uri = "https://graph.microsoft.com/v1.0/users/$FromField/mailfolders/drafts/messages"
            if ($MgGraphRequest) {
                $OutputRest = Invoke-MgGraphRequest -Method POST -Uri $Uri -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            } else {
                $OutputRest = Invoke-RestMethod -Uri $Uri -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            }
            $OutputRest
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
    }
}