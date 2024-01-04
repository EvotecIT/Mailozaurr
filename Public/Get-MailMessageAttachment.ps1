function Get-MailMessageAttachment {
    [CmdletBinding()]
    param(
        [string] $UserPrincipalName,
        [PSCredential] $Credential,
        [switch] $All,
        [int] $Limit = 10,
        [ValidateSet(
            'createdDateTime', 'lastModifiedDateTime', 'changeKey', 'categories', 'receivedDateTime', 'sentDateTime', 'hasAttachments', 'internetMessageId', 'subject', 'bodyPreview', 'importance', 'parentFolderId', 'conversationId', 'conversationIndex', 'isDeliveryReceiptRequested', 'isReadReceiptRequested', 'isRead', 'isDraft', 'webLink', 'inferenceClassification', 'body', 'sender', 'from', 'toRecipients', 'ccRecipients', 'bccRecipients', 'replyTo', 'flag')
        ][string[]] $Property,
        [string] $Filter,
        [switch] $MgGraphRequest,
        [string] $Id,
        [string] $Path
    )

    if ($MgGraphRequest) {
        # do nothing, as we're using Connect-MgGraph
    } else {
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
    }

    $QueryParameters = [ordered] @{
        filter = $Filter
        select = $Property -join ','
    }
    $joinUriQuerySplat = @{
        BaseUri               = 'https://graph.microsoft.com/v1.0'
        QueryParameter        = $QueryParameters
        RelativeOrAbsoluteUri = "/users/$UserPrincipalName/messages/$Id/attachments"
    }
    Remove-EmptyValue -Hashtable $joinUriQuerySplat -Recursive -Rerun 2

    $Uri = Join-UriQuery @joinUriQuerySplat

    Write-Verbose "Get-MailMessageAttachment - Executing $Uri"
    $OutputData = if ($All) {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET -MGGraphRequest:$MgGraphRequest.IsPresent -FullUri
    } else {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET -MGGraphRequest:$MgGraphRequest.IsPresent -FullUri | Select-Object -First $Limit
    }
    if ($OutputData) {
        foreach ($Data in $OutputData) {
            if ($Data.contentBytes -and $Path) {
                try {
                    $PathToFile = [System.IO.Path]::Combine($Path, $Data.Name)
                    $FileStream = [System.IO.FileStream]::new($PathToFile, [System.IO.FileMode]::Create)
                    $AttachedBytes = [System.Convert]::FromBase64String($Data.ContentBytes)
                    $FileStream.Write($AttachedBytes, 0, $AttachedBytes.Length)
                    $FileStream.Close()
                    Get-Item -Path $PathToFile
                } catch {
                    Write-Warning "Get-MailMessageAttachment - Couldn't save file to $Path. Error: $($_.Exception.Message)"
                }
            } else {
                $Data
            }
        }
    } else {
        Write-Verbose "Get-MailMessageAttachment - No data found"
    }
}