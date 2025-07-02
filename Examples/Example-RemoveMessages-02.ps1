Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# ------------------------------------
# Advanced IMAP and POP3 clean-up
# ------------------------------------
$imap = Connect-IMAP -Server 'imap.example.com' -UserName 'user' -Password 'pass'
$uids = Get-IMAPMessage -Client $imap -Subject 'Spam' -Limit 10 | Select-Object -ExpandProperty Uid
Remove-IMAPMessage -Client $imap -Uid $uids -Confirm:$false
Disconnect-IMAP -Client $imap

$pop = Connect-POP3 -Server 'pop.example.com' -UserName 'user' -Password 'pass'
$idx = Get-POP3Message -Client $pop -Before (Get-Date).AddDays(-7) | Select-Object -ExpandProperty Index
Remove-POP3Message -Client $pop -Index $idx -Confirm:$false
Disconnect-POP3 -Client $pop

# ------------------------------------
# Advanced Microsoft Graph deletion
# ------------------------------------
$cred = ConvertTo-GraphCredential -ClientId 'id' -ClientSecret 'secret' -DirectoryId 'tenant'
Connect-EmailGraph -Credential $cred | Out-Null
$m = Get-EmailGraphMessage -UserPrincipalName 'user@example.com' -Limit 1
Remove-GraphMessage -UserPrincipalName 'user@example.com' -MessageId $m.Id -MgGraphRequest -Confirm:$false
Disconnect-EmailGraph
