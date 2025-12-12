Describe 'Send-EmailPendingMessage' {
    BeforeAll {
        if (-not ('Mailozaurr.PendingMessageRecord' -as [type])) {
            $modulePath = Join-Path $PSScriptRoot '..' 'Mailozaurr.psd1'
            Import-Module $modulePath -Force
        }
        if (-not ('FakePendingMessageSender' -as [type])) {
            $assemblies = @(
                [Mailozaurr.PendingMessageRecord].Assembly.Location
            )
            Add-Type -ReferencedAssemblies $assemblies -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using Mailozaurr;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class FakePendingMessageSender : IPendingMessageSender {
    private static int sendCount;

    public static void Reset() => sendCount = 0;

    public static int SendCount => sendCount;

    public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        sendCount++;
        return Task.CompletedTask;
    }
}

public static class FakePendingMessageSenderFactory {
    public static PendingMessageSenderFactory Create() {
        var entries = new [] {
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.None, new FakePendingMessageSender()),
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.SendGrid, new FakePendingMessageSender()),
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.Mailgun, new FakePendingMessageSender()),
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.SES, new FakePendingMessageSender()),
            new KeyValuePair<EmailProvider, IPendingMessageSender>(EmailProvider.Gmail, new FakePendingMessageSender()),
        };
        return new PendingMessageSenderFactory(entries);
    }
}
"@
        }
    }

    BeforeEach {
        [FakePendingMessageSender]::Reset()
        $delegate = [System.Delegate]::CreateDelegate([System.Func[Mailozaurr.PendingMessageSenderFactory]], [FakePendingMessageSenderFactory], 'Create')
        [Mailozaurr.PowerShell.CmdletSendEmailPendingMessage]::SenderFactoryProvider = $delegate
    }

    AfterEach {
        [Mailozaurr.PowerShell.CmdletSendEmailPendingMessage]::SenderFactoryProvider = $null
    }

    It 'WhatIf skips sending pending messages' {
        $path = Join-Path $TestDrive 'pending-whatif'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $msg = [MimeKit.MimeMessage]::new()
        $msg.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $msg.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $msg.Subject = 'Pending'
        $msg.Body = [MimeKit.TextPart]::new('plain')
        $stream = [System.IO.MemoryStream]::new()
        $msg.WriteTo($stream)

        $record = [Mailozaurr.PendingMessageRecord]::new()
        $record.MessageId = $msg.MessageId
        $record.MimeMessage = [Convert]::ToBase64String($stream.ToArray())
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $record.NextAttemptAt = [DateTimeOffset]::UtcNow
        $record.Server = 'smtp.server'
        $record.Port = 25
        $record.Provider = [Mailozaurr.EmailProvider]::None
        $repo.SaveAsync($record).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -WhatIf

        [FakePendingMessageSender]::SendCount | Should -Be 0
        (Get-EmailPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 1
    }

    It 'Resends selected messages by id regardless of schedule' {
        $path = Join-Path $TestDrive 'pending-targeted'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $msg = [MimeKit.MimeMessage]::new()
        $msg.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $msg.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $msg.Subject = 'Pending'
        $msg.Body = [MimeKit.TextPart]::new('plain')
        $stream = [System.IO.MemoryStream]::new()
        $msg.WriteTo($stream)

        $record = [Mailozaurr.PendingMessageRecord]::new()
        $record.MessageId = $msg.MessageId
        $record.MimeMessage = [Convert]::ToBase64String($stream.ToArray())
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $record.NextAttemptAt = [DateTimeOffset]::UtcNow.AddHours(2)
        $record.Server = 'smtp.server'
        $record.Port = 25
        $repo.SaveAsync($record).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -MessageId $record.MessageId

        [FakePendingMessageSender]::SendCount | Should -Be 1
        (Get-EmailPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 0
    }

    It 'Processes only the requested provider' {
        $path = Join-Path $TestDrive 'pending-provider'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $message = [MimeKit.MimeMessage]::new()
        $message.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $message.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $message.Subject = 'Provider filter'
        $message.Body = [MimeKit.TextPart]::new('plain')
        $buffer = [System.IO.MemoryStream]::new()
        $message.WriteTo($buffer)
        $payload = [Convert]::ToBase64String($buffer.ToArray())

        $smtpRecord = [Mailozaurr.PendingMessageRecord]::new()
        $smtpRecord.MessageId = [Guid]::NewGuid().ToString()
        $smtpRecord.MimeMessage = $payload
        $smtpRecord.Timestamp = [DateTimeOffset]::UtcNow
        $smtpRecord.NextAttemptAt = [DateTimeOffset]::UtcNow
        $smtpRecord.Server = 'smtp.server'
        $smtpRecord.Port = 25
        $smtpRecord.Provider = [Mailozaurr.EmailProvider]::None
        $repo.SaveAsync($smtpRecord).GetAwaiter().GetResult()

        $apiRecord = [Mailozaurr.PendingMessageRecord]::new()
        $apiRecord.MessageId = [Guid]::NewGuid().ToString()
        $apiRecord.MimeMessage = $payload
        $apiRecord.Timestamp = [DateTimeOffset]::UtcNow
        $apiRecord.NextAttemptAt = [DateTimeOffset]::UtcNow
        $apiRecord.Provider = [Mailozaurr.EmailProvider]::Gmail
        $repo.SaveAsync($apiRecord).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -Provider ([Mailozaurr.EmailProvider]::None)

        [FakePendingMessageSender]::SendCount | Should -Be 1
        $remaining = @(Get-EmailPendingMessage -PendingMessagesPath $path)
        $remaining.Count | Should -Be 1
        $remaining[0].Provider | Should -Be ([Mailozaurr.EmailProvider]::Gmail)
    }

    It 'Replays non-SMTP messages when filtered by provider' {
        $path = Join-Path $TestDrive 'pending-api'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $message = [MimeKit.MimeMessage]::new()
        $message.From.Add([MimeKit.MailboxAddress]::Parse('sender@example.com'))
        $message.To.Add([MimeKit.MailboxAddress]::Parse('recipient@example.com'))
        $message.Subject = 'queued'
        $message.Body = [MimeKit.TextPart]::new('plain')
        $buffer = [System.IO.MemoryStream]::new()
        $message.WriteTo($buffer)
        $payload = [Convert]::ToBase64String($buffer.ToArray())

        $gmailRecord = [Mailozaurr.PendingMessageRecord]::new()
        $gmailRecord.MessageId = [Guid]::NewGuid().ToString()
        $gmailRecord.MimeMessage = $payload
        $gmailRecord.Timestamp = [DateTimeOffset]::UtcNow
        $gmailRecord.NextAttemptAt = [DateTimeOffset]::UtcNow
        $gmailRecord.Provider = [Mailozaurr.EmailProvider]::Gmail
        $repo.SaveAsync($gmailRecord).GetAwaiter().GetResult()

        $sendGridRecord = [Mailozaurr.PendingMessageRecord]::new()
        $sendGridRecord.MessageId = [Guid]::NewGuid().ToString()
        $sendGridRecord.MimeMessage = $payload
        $sendGridRecord.Timestamp = [DateTimeOffset]::UtcNow
        $sendGridRecord.NextAttemptAt = [DateTimeOffset]::UtcNow
        $sendGridRecord.Provider = [Mailozaurr.EmailProvider]::SendGrid
        $repo.SaveAsync($sendGridRecord).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -Provider ([Mailozaurr.EmailProvider]::Gmail)

        [FakePendingMessageSender]::SendCount | Should -Be 1
        $remaining = @(Get-EmailPendingMessage -PendingMessagesPath $path)
        $remaining.Count | Should -Be 1
        $remaining[0].Provider | Should -Be ([Mailozaurr.EmailProvider]::SendGrid)
    }

    It 'Honors schedule unless ProcessAll is specified' {
        $path = Join-Path $TestDrive 'pending-schedule'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $message = [MimeKit.MimeMessage]::new()
        $message.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $message.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $message.Subject = 'Future delivery'
        $message.Body = [MimeKit.TextPart]::new('plain')
        $dataStream = [System.IO.MemoryStream]::new()
        $message.WriteTo($dataStream)

        $scheduled = [Mailozaurr.PendingMessageRecord]::new()
        $scheduled.MessageId = $message.MessageId
        $scheduled.MimeMessage = [Convert]::ToBase64String($dataStream.ToArray())
        $scheduled.Timestamp = [DateTimeOffset]::UtcNow
        $scheduled.NextAttemptAt = [DateTimeOffset]::UtcNow.AddHours(4)
        $scheduled.Server = 'smtp.server'
        $scheduled.Port = 25
        $repo.SaveAsync($scheduled).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path

        [FakePendingMessageSender]::SendCount | Should -Be 0
        (Get-EmailPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 1

        [FakePendingMessageSender]::Reset()
        Send-EmailPendingMessage -PendingMessagesPath $path -ProcessAll

        [FakePendingMessageSender]::SendCount | Should -Be 1
        (Get-EmailPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 0
    }

    It 'Processes due messages without hanging when using the file repository' {
        $path = Join-Path $TestDrive 'pending-hang-check'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $message = [MimeKit.MimeMessage]::new()
        $message.From.Add([MimeKit.MailboxAddress]::Parse('a@example.com'))
        $message.To.Add([MimeKit.MailboxAddress]::Parse('b@example.com'))
        $message.Subject = 'Immediate delivery'
        $message.Body = [MimeKit.TextPart]::new('plain')
        $data = [System.IO.MemoryStream]::new()
        $message.WriteTo($data)

        $record = [Mailozaurr.PendingMessageRecord]::new()
        $record.MessageId = $message.MessageId
        $record.MimeMessage = [Convert]::ToBase64String($data.ToArray())
        $record.Timestamp = [DateTimeOffset]::UtcNow
        $record.NextAttemptAt = [DateTimeOffset]::UtcNow.AddMinutes(-1)
        $record.Provider = [Mailozaurr.EmailProvider]::None
        $repo.SaveAsync($record).GetAwaiter().GetResult()

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Send-EmailPendingMessage -PendingMessagesPath $path
        $stopwatch.Stop()

        [FakePendingMessageSender]::SendCount | Should -Be 1
        (Get-EmailPendingMessage -PendingMessagesPath $path | Measure-Object).Count | Should -Be 0
        $stopwatch.Elapsed.TotalSeconds | Should -BeLessThan 5
    }
}
