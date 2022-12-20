function New-GraphSendMessage {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [System.Diagnostics.Stopwatch] $StopWatch,
        [string] $MailSentTo,
        [string] $FromField,
        [System.Collections.IDictionary] $Authorization,
        [string] $Body,
        [switch] $Suppress,
        [switch] $MgGraphRequest
    )
    Try {
        if ($PSCmdlet.ShouldProcess("$MailSentTo", 'Send-EmailMessage')) {
            $Uri = "https://graph.microsoft.com/v1.0/users/$FromField/sendMail"
            if ($MgGraphRequest) {
                $null = Invoke-MgGraphRequest -Method POST -Uri $Uri -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            } else {
                $null = Invoke-RestMethod -Uri $Uri -Headers $Authorization -Method POST -Body $Body -ContentType 'application/json; charset=UTF-8' -ErrorAction Stop
            }
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status        = $True
                    Error         = ''
                    SentTo        = $MailSentTo
                    SentFrom      = $FromField
                    Message       = ''
                    TimeToExecute = $StopWatch.Elapsed
                }
            }
        } else {
            if (-not $Suppress) {
                [PSCustomObject] @{
                    Status        = $false
                    Error         = 'Email not sent (WhatIf)'
                    SentTo        = $MailSentTo
                    SentFrom      = $FromField
                    Message       = ''
                    TimeToExecute = $StopWatch.Elapsed
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
                Status        = $False
                Error         = if ($RestError) { "$($RestMessage) $($ErrorText)" }  else { $RestMessage }
                SentTo        = $MailSentTo
                SentFrom      = $FromField
                Message       = ''
                TimeToExecute = $StopWatch.Elapsed
            }
        }
    }
}