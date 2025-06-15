$Failed = 0

# Ensure compiled cmdlet assembly exists
$dllPath = Join-Path $PSScriptRoot 'Sources/Mailozaurr.PowerShell/bin/Debug/net8.0/Mailozaurr.PowerShell.dll'
if (-not (Test-Path $dllPath)) {
    Write-Host "Building module for tests" -ForegroundColor Yellow
    dotnet build "$PSScriptRoot/Sources/Mailozaurr.PowerShell/Mailozaurr.PowerShell.csproj" -c Debug > $null
}

function Assert-Equal([object]$Actual, [object]$Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        Write-Host "FAIL: $Message - Expected '$Expected' got '$Actual'"
        $script:Failed++
    } else {
        Write-Host "PASS: $Message"
    }
}

function Assert-Throws([scriptblock]$ScriptBlock, [Type]$ExceptionType, [string]$Message) {
    try {
        & $ScriptBlock
        Write-Host "FAIL: $Message - No exception thrown"
        $script:Failed++
    } catch {
        if ($_.Exception -is $ExceptionType -or $_.Exception.InnerException -is $ExceptionType) {
            Write-Host "PASS: $Message"
        } else {
            Write-Host "FAIL: $Message - Wrong exception $_"
            $script:Failed++
        }
    }
}

Import-Module ./Mailozaurr.psd1 -Force

# Test ConvertFromGraphCredential
$cred = [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('client@tenant','secret')
Assert-Equal $cred.ClientId 'client' 'ConvertFromGraphCredential ClientId'
Assert-Equal $cred.DirectoryId 'tenant' 'ConvertFromGraphCredential DirectoryId'
Assert-Equal $cred.ClientSecret 'secret' 'ConvertFromGraphCredential ClientSecret'

Assert-Throws { [Mailozaurr.MicrosoftGraphUtils]::ConvertFromGraphCredential('invalid','pwd') } ([System.ArgumentException]) 'ConvertFromGraphCredential invalid'

# Test Send-EmailMessage Skip Auth
$result = Send-EmailMessage -From 'noauth@example.com' -To 'recipient@example.com' -Subject 'Skip Auth Test' -Body 'test' -Server 'smtp.example.com' -Port 25 -WhatIf -Verbose 4>&1
$output = $result | Where-Object { $_ -isnot [System.Management.Automation.VerboseRecord] }
$verbose = $result | Where-Object { $_ -is [System.Management.Automation.VerboseRecord] }
$messages = $verbose | Select-Object -ExpandProperty Message
Assert-Equal ($messages -contains 'Send-EmailMessage - Skipping authentication') $true 'SkipAuth Verbose'
Assert-Equal $output.Error 'Email not sent (WhatIf)' 'SkipAuth Error'

# Test Send-EmailMessage SMTP
$Body = '<b>Test</b>'
$Text = 'Test'
$sendEmailMessageSplat = @{From=@{Name='Przemysław Kłys';Email='test@evotec.pl'};To='testing@test.pl','test@gmail.com';Server='smtp.office365.com';HTML=$Body;Text=$Text;DeliveryNotificationOption='OnSuccess';Priority='High';Subject='This is another test email 🤣😍😒💖✨🎁 我';SecureSocketOptions='Auto';Password='TempPassword';WhatIf=$true}
$Output = Send-EmailMessage @sendEmailMessageSplat -ErrorAction Stop
Assert-Equal $Output.Error 'Email not sent (WhatIf)' 'SMTP Error'
Assert-Equal $Output.SentTo 'testing@test.pl,test@gmail.com' 'SMTP SentTo'
Assert-Equal $Output.SentFrom 'test@evotec.pl' 'SMTP SentFrom'
Assert-Equal $Output.Message '' 'SMTP Message'
Assert-Equal $Output.Server 'smtp.office365.com' 'SMTP Server'
Assert-Equal $Output.Port '587' 'SMTP Port'
Assert-Equal $Output.Status $false 'SMTP Status'

# Test Send-EmailMessage Graph
$ClientID = '0fb383f1-8bfe'
$DirectoryID = 'ceb371f6-8745'
$EncryptedClientSecret = ConvertTo-SecureString -String 'VKDM_2.eC2US7pFW1' -AsPlainText -Force | ConvertFrom-SecureString
$Credential = ConvertTo-GraphCredential -ClientID $ClientID -ClientSecretEncrypted $EncryptedClientSecret -DirectoryID $DirectoryID
$sendEmailMessageSplat = @{From='random@domain.pl';To='newemail@domain.pl';Credential=$Credential;HTML=$Body;Subject='This is another test email 2';Graph=$true;Verbose=$true;Priority='Low';DoNotSaveToSentItems=$true;WhatIf=$true}
$GraphOutput = Send-EmailMessage @sendEmailMessageSplat
Assert-Equal $GraphOutput.Error 'Email not sent (WhatIf)' 'Graph Error'
Assert-Equal $GraphOutput.SentTo 'newemail@domain.pl' 'Graph SentTo'
Assert-Equal $GraphOutput.SentFrom 'random@domain.pl' 'Graph SentFrom'
Assert-Equal $GraphOutput.Message '' 'Graph Message'
Assert-Equal $GraphOutput.Status $false 'Graph Status'

# Test SMTP no TLS no credentials
$Output = Send-EmailMessage -From 'test@evotec.pl' -To 'mailozaurr@evotec.pl' -Server 'smtp.freesmtpservers.com' -Port 25 -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' -Subject '😒💖 This is another test email 我' -Verbose -WhatIf
Assert-Equal $Output.Error 'Email not sent (WhatIf)' 'SMTP no TLS Error'
Assert-Equal $Output.SentTo 'mailozaurr@evotec.pl' 'SMTP no TLS SentTo'
Assert-Equal $Output.SentFrom 'test@evotec.pl' 'SMTP no TLS SentFrom'
Assert-Equal $Output.Message '' 'SMTP no TLS Message'
Assert-Equal $Output.Server 'smtp.freesmtpservers.com' 'SMTP no TLS Server'
Assert-Equal $Output.Port 25 'SMTP no TLS Port'
Assert-Equal $Output.Status $false 'SMTP no TLS Status'

if ($Failed -gt 0) {
    throw "$Failed tests failed."
} else {
    Write-Host "All tests passed"
}
