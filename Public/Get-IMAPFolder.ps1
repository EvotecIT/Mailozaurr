function Get-IMAPFolder {
    [cmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Collections.IDictionary] $Client,
        [MailKit.FolderAccess] $FolderAccess = [MailKit.FolderAccess]::ReadOnly
    )

    $Folder = $Client.Data.Inbox
    $null = $Folder.Open($FolderAccess)

    Write-Verbose "Get-IMAPMessage - Total messages $($Folder.Count), Recent messages $($Folder.Recent)"
    $Client.Messages = $Folder
    $Client.Count = $Folder.Count
    $Client.Recent = $Folder.Recent
    $Client

}