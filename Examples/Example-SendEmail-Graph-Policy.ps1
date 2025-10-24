<#
Demonstrates throttling-safe Graph sending with retry/backoff knobs from PowerShell.

Security note: Do not hardcode client secrets in scripts. Use secure storage
such as SecretManagement or environment variables and retrieve them at runtime.
#>

$ClientId = 'your-app-id'
$ClientSecret = 'your-secret'
$TenantId = 'your-tenant-id'

$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId

Send-EmailMessage -Graph -From 'sender@example.com' -To 'recipient@example.com' `
  -Credential $cred -HTML '<b>Hello</b>' -Subject 'Graph policy demo' `
  -RetryCount 4 -RetryDelayMilliseconds 1000 -JitterMilliseconds 500 -MaxDelayMilliseconds 30000 `
  -GraphMaxConcurrency 2 -Verbose
