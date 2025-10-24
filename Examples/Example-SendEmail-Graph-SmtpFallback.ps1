<#
Configures a global SMTP fallback factory and sends via Graph with fallback enabled.
#>

# Configure once per session
[Mailozaurr.MailozaurrOptions]::SmtpFallbackFactory = {
  param($graph)
  $s = [Mailozaurr.Smtp]::new()
  $s.Connect('smtp.office365.com', 587)
  $s.Authenticate([System.Net.NetworkCredential]::new('user@example.com','password'))
  $s
}

$ClientId = 'your-app-id'
$ClientSecret = 'your-secret'
$TenantId = 'your-tenant-id'
$cred = ConvertTo-GraphCredential -ClientId $ClientId -ClientSecret $ClientSecret -DirectoryId $TenantId

Send-EmailMessage -Graph -EnableSmtpFallback -From 'sender@example.com' -To 'recipient@example.com' `
  -Credential $cred -HTML '<b>Hello via fallback</b>' -Subject 'Graph+SMTP fallback' `
  -RetryCount 4 -RetryDelayMilliseconds 1000 -JitterMilliseconds 500 -MaxDelayMilliseconds 30000

