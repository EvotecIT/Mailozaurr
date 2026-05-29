Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Login = 'sender@example.com'
$ClientID = '00000000-0000-0000-0000-000000000000'
$TenantID = '00000000-0000-0000-0000-000000000000'
$Text = 'Hello from Mailozaurr using Office 365 OAuth2.'
$Body = '<p>Hello from Mailozaurr using Office 365 OAuth2.</p>'

$CredentialOAuth2 = Connect-OAuthO365 -Login $Login -ClientID $ClientID -TenantID $TenantID

Send-EmailMessage -From @{ Name = 'Sender'; Email = $Login } -To 'recipient@example.com' `
    -Server 'smtp.office365.com' -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'OAuth2 test email' -SecureSocketOptions Auto -Credential $CredentialOAuth2 -OAuth2
