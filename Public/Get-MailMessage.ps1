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
        [string] $Filter
    )
    if ($Credential) {
        $AuthorizationData = ConvertFrom-GraphCredential -Credential $Credential
    } else {
        return
    }
    $Authorization = Connect-O365Graph -ApplicationID $AuthorizationData.ClientID -ApplicationKey $AuthorizationData.ClientSecret -TenantDomain $AuthorizationData.DirectoryID -Resource https://graph.microsoft.com

    $Uri = "/users/$UserPrincipalName/messages"
    $Addon = '?'
    if ($Property) {
        $Poperties = $Property -join ','
        $Addon = -join ($Addon, "`$Select=$Poperties")
    }
    if ($Filter) {
        $Addon = -join ($Addon, "&`$filter=$Filter")
    }
    #Write-Verbose $Addon
    #$Addon = [System.Web.HttpUtility]::UrlEncode($Addon)
    if ($Addon.Length -gt 1) {
        $Uri = -join ($Uri, $Addon)
    }

    Write-Verbose "Get-MailMessage - Executing $Uri"
    $Uri = [uri]::EscapeUriString($Uri)
    Write-Verbose "Get-MailMessage - Executing $Uri"
    if ($All) {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET
    } else {
        Invoke-O365Graph -Headers $Authorization -Uri $Uri -Method GET | Select-Object -First $Limit
    }
}

