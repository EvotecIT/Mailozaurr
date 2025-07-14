Import-Module .\Mailozaurr.psd1 -Force
#Import-Module Microsoft.Graph.Authentication -Force

# this shows how to send email using combination of Mailozaurr and Microsoft.Graph to use Connect-MgGraph to authorize
$Body = EmailBody {
    New-HTMLText -Text "This is test of Connect-MGGraph functionality"
}

$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/welcome-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/simple-confirmation-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/password-reset-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/order-confirmation-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/newsletter-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/invoice-email.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/corrected-email-pattern.html" -Raw
#$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/direct-email-pattern.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/layout-demo-1-extra-wide.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/base64-embedding-demo.html" -Raw
$Body = Get-Content -Path "G:/GitHub/HtmlForgeX/HtmlForgeX.Examples/bin/Debug/net8.0/improved-consistency-demo.html" -Raw

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