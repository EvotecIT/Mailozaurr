Import-Module .\Mailozaurr.psd1 -Force
Import-Module Microsoft.Graph.Authentication -Force

# this shows how to send email using combination of Mailozaurr and Microsoft.Graph to use Connect-MgGraph to authorize
$Body = EmailBody {
    New-HTMLText -Text "This is test of Connect-MGGraph functionality"
}

# authorize via Connect-MgGraph with delegated rights or any other supported method
Connect-MgGraph -Scopes Mail.Send

# sending email
$sendEmailMessageSplat = @{
    From           = 'przemyslaw.klys@test.pl'
    To             = 'przemyslaw.klys@test.pl'
    HTML           = $Body
    Subject        = 'This tests email as delegated'
    MgGraphRequest = $true
    Verbose        = $true
}
Send-EmailMessage @sendEmailMessageSplat