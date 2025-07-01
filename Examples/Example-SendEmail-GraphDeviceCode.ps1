Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientId = 'Your-Application-ID'
$TenantId = 'Your-Tenant-ID'
$Scopes = @('Mail.ReadWrite','Mail.Send')

# Sign in interactively using device code
$graph = Connect-EmailGraph -ClientId $ClientId -DirectoryId $TenantId -DeviceCode -Scopes $Scopes

$Body = EmailBody {
    EmailText -Text 'Hello from device code authentication!'
} -Online

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' \
    -Subject 'Device Code Graph Test' -HTML $Body -Graph -Verbose
