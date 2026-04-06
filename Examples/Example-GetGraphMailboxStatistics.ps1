Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$graph = Connect-EmailGraph -ClientId 'id' -DirectoryId 'tenant' -ClientSecretSecretName 'graph-client-secret' -ClientSecretVaultName 'MailSecrets'

Get-GraphMailboxStatistics -UserPrincipalName 'user@example.com' -Connection $graph |
    Select-Object UserPrincipalName, MessageCount, MessagesWithAttachments, TotalAttachmentSize, TotalFolders, FolderStatistics |
    Export-Csv -NoTypeInformation 'mailbox-stats.csv'
