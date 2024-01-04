function Get-MailMessage {
    [cmdletBinding()]
    param(
        [string] $UserPrincipalName,
        [PSCredential] $Credential,
        [switch] $All,
        [int] $Limit = 10,
        [ValidateSet(
            'createdDateTime', 'lastModifiedDateTime', 'changeKey', 'categories', 'receivedDateTime', 'sentDateTime', 'hasAttachments', 'internetMessageId', 'subject', 'bodyPreview', 'importance', 'parentFolderId', 'conversationId', 'conversationIndex', 'isDeliveryReceiptRequested', 'isReadReceiptRequested', 'isRead', 'isDraft', 'webLink', 'inferenceClassification', 'body', 'sender', 'from', 'toRecipients', 'ccRecipients', 'bccRecipients', 'replyTo', 'flag')
        ][string[]] $Property,
        [string] $Filter,
        [switch] $MgGraphRequest
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
        RelativeOrAbsoluteUri = "/users/$UserPrincipalName/messages"
    }
    Remove-EmptyValue -Hashtable $joinUriQuerySplat -Recursive -Rerun 2

    $Uri = Join-UriQuery @joinUriQuerySplat

    Write-Verbose "Get-MailMessage - Executing $Uri"
    if ($All) {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET -MGGraphRequest:$MgGraphRequest.IsPresent -FullUri
    } else {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET -MGGraphRequest:$MgGraphRequest.IsPresent -FullUri | Select-Object -First $Limit
    }
}

