Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

# It seems larger HTML is not supported. Online makes sure it uses less libraries inline
# it may be related to not escaping chars properly for JSON, may require investigation
$Body = EmailBody {
    EmailText -Text 'This is my text'
    EmailTable -DataTable (Get-Process | Select-Object -First 5 -Property Name, Id, PriorityClass, CPU, Product)
} -Online

# Credentials for Graph
$ClientID = '0fb383f1'
$DirectoryID = 'ceb371f6'
$ClientSecret = 'VKDM_'

$Credential = ConvertTo-GraphCredential -ClientID $ClientID -ClientSecret $ClientSecret -DirectoryID $DirectoryID

# Sending email
Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@test1.pl' } -To 'przemyslaw.klys@test.pl' `
    -Credential $Credential -HTML $Body -Subject 'This is another test email 1' -Graph -Verbose -Priority High

# sending email with From as string (it won't matter for Exchange )
Send-EmailMessage -From 'przemyslaw.klys@test1.pl' -To 'przemyslaw.klys@test2.pl' `
    -Credential $Credential -HTML $Body -Subject 'This is another test email 2' -Graph -Verbose -Priority Low