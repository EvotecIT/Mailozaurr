<#
Configures a global SMTP fallback factory and sends via Graph with fallback enabled.
#>

# WARNING: Do not hardcode credentials in scripts. Use Get-Credential, environment variables
# or a secure secret store (e.g., SecretManagement) for production scenarios.
$ClientId = 'your-app-id'
$ClientSecret = 'your-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId

# Configure SMTP fallback for this session using application-safe credential retrieval.
$smtpCredential = Get-Credential -Message 'SMTP fallback credential'
Set-MailozaurrSmtpFallback -Server 'smtp.office365.com' -Port 587 -Credential $smtpCredential

Send-EmailMessage -Graph -EnableSmtpFallback -From 'sender@example.com' -To 'recipient@example.com' `
  -Credential $cred -HTML '<b>Hello via fallback</b>' -Subject 'Graph+SMTP fallback' `
  -RetryCount 4 -RetryDelayMilliseconds 1000 -JitterMilliseconds 500 -MaxDelayMilliseconds 30000
