Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
$graph = Connect-EmailGraph -Credential $cred

Get-GraphMailboxStatistics -UserPrincipalName 'user@example.com' -Connection $graph |
    Export-Csv -NoTypeInformation 'mailbox-stats.csv'
