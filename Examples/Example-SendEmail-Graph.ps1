Import-Module $PSScriptRoot\..\Mailozaurr.psd1 -Force

$Body = EmailBody {
    EmailText -Text 'This is my text'
    EmailTable -DataTable (Get-Process | Select-Object -First 5 -Property Name, Id, PriorityClass, CPU, Product)
} -Online

# Credentials for Graph
$ClientID = 'f8f134f3-78c7-48f4-a371-5d6eefa447cd'
$DirectoryID = 'ceb371f6-8745-4876-a040-69f2d10a9d1a'
$ClientSecret = Read-Host 'Graph client secret' -AsSecureString

$Credential = ConvertTo-GraphCredential -ClientID $ClientID -ClientSecretSecureString $ClientSecret -DirectoryID $DirectoryID

# Sending email
Send-EmailMessage -From @{ Name = 'Przemysław Kłys'; Email = 'przemyslaw.klys@evotec.pl' } -To 'przemyslaw.klys@evotec.pl' `
    -Credential $Credential -HTML $Body -Subject 'This is another test email 1' -Graph -Verbose -Priority High

# sending email with From as string (it won't matter for Exchange )
Send-EmailMessage -From 'przemyslaw.klys@evotec.pl' -To 'przemyslaw.klys@evotec.pl' `
    -Credential $Credential -HTML $Body -Subject 'This is another test email 2' -Graph -Verbose -Priority Low
