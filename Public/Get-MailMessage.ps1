function Get-MailMessage {
    [cmdletBinding()]
    param(
        [string] $UserPrincipalName,
        [PSCredential] $Credential,
        [switch] $All,
        [int] $Limit = 10
    )
    if ($Credential) {
        $AuthorizationData = ConvertFrom-GraphCredential -Credential $Credential
    } else {
        return
    }
    $Authorization = Connect-O365Graph -ApplicationID $AuthorizationData.ClientID -ApplicationKey $AuthorizationData.ClientSecret -TenantDomain $AuthorizationData.DirectoryID -Resource https://graph.microsoft.com

    if ($All) {
        Invoke-O365Graph -Headers $Authorization -Uri "/users/$UserPrincipalName/messages" -Method GET
    } else {
        Invoke-O365Graph -Headers $Authorization -Uri "/users/$UserPrincipalName/messages" -Method GET | Select-Object -First $Limit
    }
}

