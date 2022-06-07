Describe 'Send-EmailMessage' {
    It 'Send email using given parameters (SMTP)' {
        $sendEmailMessageSplat = @{
            From                       = @{ Name = 'Przemysław Kłys'; Email = 'test@evotec.pl' }
            To                         = 'testing@test.pl', 'test@gmail.com'
            Server                     = 'smtp.office365.com'
            HTML                       = $Body
            Text                       = $Text
            DeliveryNotificationOption = 'OnSuccess'
            Priority                   = 'High'
            Subject                    = 'This is another test email 🤣😍😒💖✨🎁 我'
            SecureSocketOptions        = 'Auto'
            Password                   = 'TempPassword'
            WhatIf                     = $True
        }

        $Output = Send-EmailMessage @sendEmailMessageSplat -ErrorAction Stop
        $Output.Error | Should -Be 'Email not sent (WhatIf)'
        $Output.SentTo | Should -Be 'testing@test.pl, test@gmail.com'
        $Output.SentFrom.Email | Should -Be 'test@evotec.pl'
        $Output.SentFrom.Name | Should -Be 'Przemysław Kłys'
        $Output.Message | Should -Be $null
        $Output.Server | Should -Be 'smtp.office365.com'
        $Output.Port | Should -Be '587'
        $Output.Status | Should -Be $false
    }
    It 'Send email using given parameters (Graph)' {
        # Credentials for Graph
        $ClientID = '0fb383f1-8bfe'
        $DirectoryID = 'ceb371f6-8745'

        $EncryptedClientSecret = ConvertTo-SecureString -String 'VKDM_2.eC2US7pFW1' -AsPlainText -Force | ConvertFrom-SecureString

        $Credential = ConvertTo-GraphCredential -ClientID $ClientID -ClientSecretEncrypted $EncryptedClientSecret -DirectoryID $DirectoryID

        # sending email
        $sendEmailMessageSplat = @{
            From                 = 'random@domain.pl'
            To                   = 'newemail@domain.pl'
            Credential           = $Credential
            HTML                 = $Body
            Subject              = 'This is another test email 2'
            Graph                = $true
            Verbose              = $true
            Priority             = 'Low'
            DoNotSaveToSentItems = $true
            WhatIf               = $true
        }
        $GraphOutput = Send-EmailMessage @sendEmailMessageSplat
        $GraphOutput.Error | Should -Be 'Email not sent (WhatIf)'
        $GraphOutput.SentTo | Should -Be 'newemail@domain.pl'
        $GraphOutput.SentFrom | Should -Be 'random@domain.pl'
        $GraphOutput.Message | Should -Be ''
        $GraphOutput.Status | Should -Be $False
    }
    It 'Send email using given parameters (SMTP no TLS, no login/password)' {
        $Output = Send-EmailMessage -From 'test@evotec.pl' -To 'mailozaurr@evotec.pl' -Server 'smtp.freesmtpservers.com' -Port 25 -Body 'test me 🤣😍😒💖✨🎁 Przemysław Kłys' -Subject '😒💖 This is another test email 我' -Verbose
        $Output.Error | Should -Be ''
        $Output.SentTo | Should -Be 'mailozaurr@evotec.pl'
        $Output.SentFrom | Should -Be 'test@evotec.pl'
        $Output.Message | Should -Be 'OK'
        $Output.Server | Should -Be 'smtp.freesmtpservers.com'
        $Output.Port | Should -Be 25
        $Output.Status | Should -Be $true
    }
}