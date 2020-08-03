Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$ClientID = '939333074185'
$ClientSecret = 'gk2ztAGU'

$CredentialOAuth2 = Connect-oAuthGoogle -ClientID $ClientID -ClientSecret $ClientSecret -GmailAccount 'evot@gmail.com'

Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'evot@gmail.com' } -To 'test@evotec.pl' `
    -Server 'smtp.gmail.com' -HTML $Body -Text $Text -DeliveryNotificationOption OnSuccess -Priority High `
    -Subject 'This is another test email' -SecureSocketOptions Auto -Credential $CredentialOAuth2 -oAuth