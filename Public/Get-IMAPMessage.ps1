function Get-IMAPMessage {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory=$true)][System.Collections.IDictionary] $Client,
        [MailKit.FolderAccess] $FolderAccess = [MailKit.FolderAccess]::ReadOnly,
        [string]$From,
        [string]$To,
        [string]$SubjectContains,
        [string]$BodyContains
    )
    if ($Client) {

        # Open inbox folder
        $Folder = $Client.Data.Inbox
        $Folder.Open($FolderAccess) | Out-Null
        Write-Verbose "Get-IMAPMessage - Total messages $($Folder.Count), Recent messages $($Folder.Recent)"
        $Client.Folder = $Folder
        
        # Get messages
        $messagesList = New-Object 'System.Collections.Generic.List[psobject]'
        foreach ($message in $Folder)
        {
            $currentMessage = [PSCustomObject]@{
                Id      = $message.MessageId 
                From    = $message.From
                To      = $message.To
                CC      = $message.CC
                BCC     = $message.BCC
                Date    = $message.Date
                Subject = $message.Subject
                HtmlBody    = $message.HtmlBody
                TextBody    = $message.TextBody
                Importance  = $message.Importance
                Priority    = $message.Priority
                Sender      = $message.Sender
                Headers     = $message.Headers
                Attachments = $message.Attachments
            }
            $messagesList.Add($currentMessage)           
        }

        # Return message list
        $messagesList | Where-Object { ($_.From -like "*$From*") `
            -and ($_.To -like "*$To*") `
            -and ($_.Subject -like "*$SubjectContains*") `
            -and ($_.BodyHtml -like "*$BodyContains*") `
        }

    } else {
        Write-Verbose 'Get-IMAPMessage - Client not connected?'
    }
}