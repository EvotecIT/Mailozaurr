function Get-MailFolder {
    [cmdletBinding()]
    param(
        [string] $UserPrincipalName,
        [PSCredential] $Credential
    )
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
    Invoke-O365Graph -Headers $Authorization -Uri "/users/$UserPrincipalName/mailFolders" -Method GET
}