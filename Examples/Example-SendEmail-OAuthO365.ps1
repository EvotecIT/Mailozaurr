Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '4c1197dd-53'
$TenantID = 'ceb371f6-87'

$CredentialOAuth2 = Connect-oAuthO365 -ClientID $ClientID -TenantID $TenantID

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'test@evotec.pl' } -To 'test@evotec.pl' `
    -Server 'smtp.office365.com' -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'This is another test email' -SecureSocketOptions Auto -Credential $CredentialOAuth2 -oAuth2