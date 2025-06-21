Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force
$ClientId = 'your-client-id'
$TenantId = 'your-tenant-id'
$CertPath = 'path-to-your.pfx'
$CertPassword = 'your-cert-password'

$Credential = ConvertTo-GraphCertificateCredential -ClientId $ClientId -TenantId $TenantId -CertificatePath $CertPath -CertificatePassword $CertPassword

Send-EmailMessage -From 'sender@yourtenant.onmicrosoft.com' -To 'recipient@example.com' `
    -Credential $Credential -Subject 'Certificate Graph Test' `
    -HTML '<p>Hello from Graph using certificate auth!</p>' -Graph -Verbose
