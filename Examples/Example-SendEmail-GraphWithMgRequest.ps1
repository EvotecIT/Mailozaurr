Import-Module .\Mailozaurr.psd1 -Force
#Import-Module Microsoft.Graph.Authentication -Force

# this shows how to send email using combination of Mailozaurr and Microsoft.Graph to use Connect-MgGraph to authorize
$Body = EmailBody {
    New-HTMLText -Text "This is test of Connect-MGGraph functionality"
}

# Save the rendered HtmlForgeX.Email HTML beside this example before running it.
# Email source and examples live in EvotecIT/HtmlForgeX under HtmlForgeX.Email.Examples.
$Path = Join-Path $PSScriptRoot 'EmailProductLaunchAnnouncement.html'
$Body = Get-Content -Path $Path -Raw
# authorize via Connect-MgGraph with delegated rights or any other supported method
Connect-MgGraph -Scopes Mail.Send -NoWelcome

# sending email
$sendEmailMessageSplat = @{
    From           = 'przemyslaw.klys@evotec.pl'
    To             = 'przemyslaw.klys@evotec.pl'
    HTML           = $Body
    Subject        = 'This tests email as delegated'
    MgGraphRequest = $true
    Verbose        = $true
}
Send-EmailMessage @sendEmailMessageSplat