Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Credential = ConvertTo-GraphCredential -ClientID 'client-id' -ClientSecret 'secret' -DirectoryID 'tenant-id'

Send-EmailMessage -From 'sender@example.com' -To 'recipient@example.com' -Subject 'Graph Header Test' -Body 'Hello from Graph' -Graph -Credential $Credential -Headers @{ 'X-Tracking-ID' = 'abc123'; 'X-Source' = 'Mailozaurr' }
