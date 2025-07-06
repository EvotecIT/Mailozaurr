Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
$graph = Connect-EmailGraph -Credential $cred

$stats = Get-GraphMailboxStatistics -UserPrincipalName 'user@example.com' -Connection $graph

$stats | Format-List

foreach ($folder in $stats.FolderStatistics) {
    "{0,-30} {1,5} unread: {2,5}" -f $folder.DisplayName, $folder.TotalItemCount, $folder.UnreadItemCount
}
