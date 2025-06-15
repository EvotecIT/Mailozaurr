Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force
$ClientId = 'your-client-id'
$TenantId = 'your-tenant-id'
$CertPath = 'path-to-your.pfx'
$CertPassword = 'your-cert-password'

$Credential = ConvertTo-GraphCertificateCredential -ClientId $ClientId -TenantId $TenantId -CertificatePath $CertPath -CertificatePassword $CertPassword
$Token = $Credential.GetNetworkCredential().Password

$Graph = [Mailozaurr.Graph]::new()
$Graph.From = 'sender@yourtenant.onmicrosoft.com'
$Graph.To = 'recipient@example.com'
$Graph.Subject = 'Certificate Graph Test'
$Graph.HTML = '<p>Hello from Graph using certificate auth!</p>'
$Graph.AccessToken = $Token
$Graph.TokenType = 'Bearer'

$Result = $Graph.SendMessageAsync().GetAwaiter().GetResult()
$Result
