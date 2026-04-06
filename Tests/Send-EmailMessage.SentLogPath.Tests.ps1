Describe 'Send-EmailMessage - SentLogPath OptIn' {
    BeforeAll {
        if (-not ('FakeClientSentLogPs' -as [type])) {
            $refs = @(
                [Mailozaurr.Smtp].Assembly.Location,
                [Mailozaurr.ClientSmtp].Assembly.Location,
                [MailKit.Security.SecureSocketOptions].Assembly.Location,
                [MimeKit.MimeMessage].Assembly.Location
            )
            Add-Type -ReferencedAssemblies $refs -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using MailKit;
using MailKit.Security;
using MimeKit;
using System;
using System.Threading;
using System.Threading.Tasks;

public class FakeClientSentLogPs : ClientSmtp {
    private bool _connected;

    public override bool IsConnected {
        get { return _connected; }
    }

    public override void Connect(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken) {
        _connected = true;
    }

    public override void Disconnect(bool quit, CancellationToken cancellationToken) {
        _connected = false;
    }

    public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress) {
        if (string.IsNullOrWhiteSpace(message.MessageId)) {
            message.MessageId = "fake-" + Guid.NewGuid().ToString("N") + "@mailozaurr.test";
        }

        return Task.FromResult(message.MessageId);
    }
}
public static class FakeClientSentLogPsFactory {
    public static ClientSmtp Create(ProtocolLogger logger) => new FakeClientSentLogPs();
    public static void Install() => Smtp.ClientFactory = Create;
}
"@
        }
    }

    It 'Does not create default temp sent log when SentLogPath is not provided' {
        $oldTemp = $env:TEMP
        $oldTmp = $env:TMP

        try {
            $tempDir = Join-Path $TestDrive 'tmp-default'
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
            $env:TEMP = $tempDir
            $env:TMP = $tempDir

            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [FakeClientSentLogPsFactory]::Install()

            $result = Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.example.com' -Port 25 -Subject 'Subject' -Text 'Body'

            $result.Status | Should -BeTrue
            $defaultSentLogPath = Join-Path $tempDir 'Mailozaurr\sentlog.json'
            (Test-Path $defaultSentLogPath) | Should -BeFalse
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            $env:TEMP = $oldTemp
            $env:TMP = $oldTmp
        }
    }

    It 'Creates sent log when SentLogPath is provided' {
        $oldTemp = $env:TEMP
        $oldTmp = $env:TMP

        try {
            $tempDir = Join-Path $TestDrive 'tmp-optin'
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
            $env:TEMP = $tempDir
            $env:TMP = $tempDir

            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            [FakeClientSentLogPsFactory]::Install()

            $sentLogPath = Join-Path $TestDrive 'sent\sentlog.json'
            $result = Send-EmailMessage -From 'from@example.com' -To 'to@example.com' -Server 'smtp.example.com' -Port 25 -Subject 'Subject' -Text 'Body' -SentLogPath $sentLogPath

            $result.Status | Should -BeTrue
            (Test-Path $sentLogPath) | Should -BeTrue

            $records = Get-Content -Path $sentLogPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            $records.Count | Should -BeGreaterThan 0
        } finally {
            [Mailozaurr.Smtp]::ResetClientFactory()
            [Mailozaurr.SmtpConnectionPool]::ClearConnectionPool()
            $env:TEMP = $oldTemp
            $env:TMP = $oldTmp
        }
    }
}
