function global:New-PendingRecord {
    param(
        [Mailozaurr.EmailProvider]$Provider,
        [string]$MessageId = ([Guid]::NewGuid().ToString())
    )

    $message = [MimeKit.MimeMessage]::new()
    $message.From.Add([MimeKit.MailboxAddress]::Parse('sender@example.com'))
    $message.To.Add([MimeKit.MailboxAddress]::Parse('recipient@example.com'))
    $message.Subject = 'Queue item'
    $message.Body = [MimeKit.TextPart]::new('plain', 'body')
    $buffer = [System.IO.MemoryStream]::new()
    $message.WriteTo($buffer)
    $payload = [Convert]::ToBase64String($buffer.ToArray())

    $record = [Mailozaurr.PendingMessageRecord]::new()
    $record.MessageId = $MessageId
    $record.MimeMessage = $payload
    $record.Timestamp = [DateTimeOffset]::UtcNow
    $record.NextAttemptAt = [DateTimeOffset]::UtcNow
    $record.Provider = $Provider
    if ($Provider -eq [Mailozaurr.EmailProvider]::None) {
        $record.Server = 'smtp.server'
        $record.Port = 25
    }

    return $record
}

Describe 'Send-EmailPendingMessage provider and message filters' {
    BeforeAll {
        $moduleRoot = Join-Path (Join-Path $PSScriptRoot '..') '..'
        $modulePath = Join-Path $moduleRoot 'Mailozaurr.psd1'
        if (-not (Get-Module -Name Mailozaurr)) {
            Import-Module $modulePath -Force
        }

        if (-not ('FilterRecordingPendingMessageSender' -as [type])) {
            $sharedRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.dotnet/shared/Microsoft.NETCore.App'
            $runtimeVersion = Get-ChildItem -Path $sharedRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
            if (-not $runtimeVersion) {
                throw 'Unable to locate .NET runtime assemblies required for Add-Type.'
            }

            $runtimePath = $runtimeVersion.FullName
            $assemblies = @(
                [Mailozaurr.PendingMessageRecord].Assembly.Location,
                (Join-Path $runtimePath 'System.Private.CoreLib.dll'),
                (Join-Path $runtimePath 'System.Runtime.dll'),
                (Join-Path $runtimePath 'System.Collections.dll')
            )

            Add-Type -ReferencedAssemblies $assemblies -CompilerOptions '/nowarn:1701,1702' -TypeDefinition @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mailozaurr;

public sealed class FilterRecordingPendingMessageSender : IPendingMessageSender {
    private readonly EmailProvider provider;
    private static readonly Hashtable Processed = new();

    public FilterRecordingPendingMessageSender(EmailProvider provider) {
        this.provider = provider;
    }

    public static void Reset() => Processed.Clear();

    public static string[] GetProcessedMessageIds(EmailProvider provider) {
        if (Processed[provider] is ArrayList list) {
            var result = new string[list.Count];
            list.CopyTo(result, 0);
            return result;
        }

        return Array.Empty<string>();
    }

    public Task SendAsync(PendingMessageRecord record, CancellationToken ct) {
        if (Processed[provider] is not ArrayList list) {
            list = new ArrayList();
            Processed[provider] = list;
        }

        list.Add(record.MessageId ?? string.Empty);

        return Task.CompletedTask;
    }
}

public static class FilterPendingMessageSenderFactory {
    private static readonly EmailProvider[] Providers = new[] {
        EmailProvider.None,
        EmailProvider.SendGrid,
        EmailProvider.Mailgun,
        EmailProvider.SES,
        EmailProvider.Gmail
    };

    public static PendingMessageSenderFactory Create() {
        var entries = new KeyValuePair<EmailProvider, IPendingMessageSender>[Providers.Length];
        for (var i = 0; i < Providers.Length; i++) {
            var provider = Providers[i];
            entries[i] = new KeyValuePair<EmailProvider, IPendingMessageSender>(provider, new FilterRecordingPendingMessageSender(provider));
        }

        return new PendingMessageSenderFactory(entries);
    }
}
"@
        }
    }

    BeforeEach {
        [FilterRecordingPendingMessageSender]::Reset()
        $delegate = [System.Delegate]::CreateDelegate(
            [System.Func[Mailozaurr.PendingMessageSenderFactory]],
            [FilterPendingMessageSenderFactory],
            'Create')
        [Mailozaurr.PowerShell.CmdletSendEmailPendingMessage]::SenderFactoryProvider = $delegate
    }

    AfterEach {
        [Mailozaurr.PowerShell.CmdletSendEmailPendingMessage]::SenderFactoryProvider = $null
    }

    It 'Processes only messages for the requested provider' {
        $path = Join-Path $TestDrive 'filters-provider'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $smtpRecord = New-PendingRecord -Provider ([Mailozaurr.EmailProvider]::None)
        $repo.SaveAsync($smtpRecord).GetAwaiter().GetResult()
        $sesRecord = New-PendingRecord -Provider ([Mailozaurr.EmailProvider]::SES)
        $repo.SaveAsync($sesRecord).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -Provider ([Mailozaurr.EmailProvider]::SES)

        [FilterRecordingPendingMessageSender]::GetProcessedMessageIds([Mailozaurr.EmailProvider]::SES).Count | Should -Be 1
        [FilterRecordingPendingMessageSender]::GetProcessedMessageIds([Mailozaurr.EmailProvider]::None).Count | Should -Be 0

        $remaining = @(Get-EmailPendingMessage -PendingMessagesPath $path)
        $remaining.Count | Should -Be 1
        $remaining[0].MessageId | Should -Be $smtpRecord.MessageId
    }

    It 'Processes only the message matching both provider and message id filters' {
        $path = Join-Path $TestDrive 'filters-combined'
        $options = [Mailozaurr.PendingMessageRepositoryOptions]::new()
        $options.DirectoryPath = $path
        $repo = [Mailozaurr.FilePendingMessageRepository]::new($options)

        $target = New-PendingRecord -Provider ([Mailozaurr.EmailProvider]::SendGrid)
        $repo.SaveAsync($target).GetAwaiter().GetResult()
        $other = New-PendingRecord -Provider ([Mailozaurr.EmailProvider]::SendGrid)
        $repo.SaveAsync($other).GetAwaiter().GetResult()
        $mailgun = New-PendingRecord -Provider ([Mailozaurr.EmailProvider]::Mailgun)
        $repo.SaveAsync($mailgun).GetAwaiter().GetResult()

        Send-EmailPendingMessage -PendingMessagesPath $path -Provider ([Mailozaurr.EmailProvider]::SendGrid) -MessageId $target.MessageId

        $processed = [FilterRecordingPendingMessageSender]::GetProcessedMessageIds([Mailozaurr.EmailProvider]::SendGrid)
        $processed | Should -Contain $target.MessageId
        $processed.Count | Should -Be 1
        [FilterRecordingPendingMessageSender]::GetProcessedMessageIds([Mailozaurr.EmailProvider]::Mailgun).Count | Should -Be 0

        $pending = @(Get-EmailPendingMessage -PendingMessagesPath $path)
        $pending.Count | Should -Be 2
        $pending.MessageId | Should -Contain $other.MessageId
        $pending.MessageId | Should -Contain $mailgun.MessageId
    }
}
