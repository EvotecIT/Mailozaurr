using System.Diagnostics;
using System.Text;
using Mailozaurr.PowerShell;

namespace Mailozaurr.Tests;

[Collection("GraphCollection")]
public sealed class PowerShellSmtpDeliveryTests {
    [Fact]
    public async Task Command_BindsTypedServerListAndWhatIfOpensNoConnections() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        RunCommand($$"""
            $command = Get-Command Send-EmailMessage
            if ($command.Parameters['Server'].ParameterType -ne [string[]]) { throw 'Server must accept a list' }
            if ($command.Parameters['Strategy'].ParameterType -ne [Mailozaurr.SmtpServerStrategy]) { throw 'Strategy must be typed' }
            if ($command.Parameters['DeliverySession'].ParameterType -ne [Mailozaurr.SmtpDeliverySession]) { throw 'Session must be typed' }
            $result = Send-EmailMessage -From from@example.test -To to@example.test -SmtpServer @('smtp://127.0.0.1:{{first.Port}}', 'smtp://127.0.0.1:{{second.Port}}') -Strategy RoundRobin -Text body -WhatIf -ErrorAction Stop
            if ($result.Status -or $result.Error -ne 'Email not sent (WhatIf)') { throw 'WhatIf result is incorrect' }
            Write-Output 'COMMAND_PROOF_PASSED'
            """);
        Assert.Equal(0, first.Connections);
        Assert.Equal(0, second.Connections);
    }

    [Fact]
    public async Task Command_ReusesCallerSessionAcrossInvocationsAndRefreshesSentLog() {
        await using var first = new LoopbackSmtpServer();
        await using var second = new LoopbackSmtpServer();
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-PowerShell-Smtp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            RunCommand($$"""
                $session = [Mailozaurr.SmtpDeliverySession]::new()
                try {
                    $arguments = @{ From = 'from@example.test'; To = 'to@example.test'; Server = @('smtp://127.0.0.1:{{first.Port}}', 'smtp://127.0.0.1:{{second.Port}}'); Strategy = 'RoundRobin'; SecureSocketOptions = 'None'; DeliverySession = $session; Text = 'body'; ErrorAction = 'Stop' }
                    foreach ($number in 1..3) {
                        $arguments.Subject = "message-$number"
                        $arguments.SentLogPath = Join-Path '{{Quote(directory)}}' "sent-$number.json"
                        $result = Send-EmailMessage @arguments
                        if (-not $result.DeliveryAccepted -or $result.Attempts.Count -ne 1) { throw 'Command delivery failed' }
                        $record = Get-Content $arguments.SentLogPath | ConvertFrom-Json
                        if ($record.Subject -ne "message-$number") { throw 'Sent log retained previous message configuration' }
                    }
                } finally { $session.Dispose() }
                $single = Send-EmailMessage -From from@example.test -To to@example.test -Server 'smtp://127.0.0.1:{{first.Port}}' -Text body -ErrorAction Stop
                if (-not $single.DeliveryAccepted -or $single.Attempts.Count -ne 1) { throw 'Single URI did not select a delivery session' }
                Write-Output 'COMMAND_PROOF_PASSED'
                """);
            Assert.Equal(3, first.Accepted);
            Assert.Equal(1, second.Accepted);
            Assert.Equal(2, first.Connections);
            Assert.Equal(1, second.Connections);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Command_ErrorActionStopReturnsPermanentFailureWithAttempts() {
        await using var primary = new LoopbackSmtpServer(recipientCode: 550);
        await using var secondary = new LoopbackSmtpServer();
        RunCommand($$"""
            $result = Send-EmailMessage -From from@example.test -To to@example.test -Server @('smtp://127.0.0.1:{{primary.Port}}', 'smtp://127.0.0.1:{{secondary.Port}}') -Text body -RetryCount 4 -RetryAlways -ErrorAction Stop
            if ($result.Status -or $result.DeliveryAccepted -or $result.IsTransient -or $result.DeliveryUnknown) { throw 'Permanent rejection was not preserved' }
            if ($result.Attempts.Count -ne 1 -or $result.Attempts[0].Port -ne {{primary.Port}} -or [string]::IsNullOrEmpty($result.Error)) { throw 'Failure attempt metadata was lost' }
            $single = Send-EmailMessage -From from@example.test -To to@example.test -Server '127.0.0.1' -Port {{primary.Port}} -SecureSocketOptions None -CircuitBreakMinutes 0 -Text body -ErrorAction Stop
            if ($single.Status -or $single.Attempts.Count -ne 1) { throw 'Explicit circuit settings did not select a delivery session' }
            Write-Output 'COMMAND_PROOF_PASSED'
            """);
        Assert.Equal(2, primary.Recipients);
        Assert.Equal(0, secondary.Connections);
    }

    [Fact]
    public async Task Command_MimeSaveFailurePreservesAcceptedResultWithErrorActionStop() {
        await using var primary = new LoopbackSmtpServer();
        await using var secondary = new LoopbackSmtpServer();
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr-PowerShell-Mime-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try {
            RunCommand($$"""
                $results = @(Send-EmailMessage -From from@example.test -To to@example.test -Server @('smtp://127.0.0.1:{{primary.Port}}', 'smtp://127.0.0.1:{{secondary.Port}}') -Text body -RetryCount 4 -MimeMessagePath '{{Quote(directory)}}' -ErrorAction Stop)
                if ($results.Count -ne 1) { throw 'Expected one final result' }
                $result = $results[0]
                if (-not $result.Status -or -not $result.DeliveryAccepted -or $result.Attempts.Count -ne 1) { throw 'Accepted delivery was lost after MIME save failure' }
                if ($result.PostSendErrors.Count -ne 1 -or $result.PostSendErrors[0] -notlike 'MIME save:*') { throw 'MIME save error was not retained' }
                Write-Output 'COMMAND_PROOF_PASSED'
                """);
            Assert.Equal(1, primary.Accepted);
            Assert.Equal(0, secondary.Connections);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Quote(string value) => value.Replace("'", "''");

    private static void RunCommand(string body) {
        string assembly = typeof(CmdletSendEmailMessage).Assembly.Location;
        string script = $"$ErrorActionPreference = 'Stop'; Import-Module '{Quote(assembly)}'; " + body;
#if NETFRAMEWORK
        const string executable = "powershell.exe";
#else
        const string executable = "pwsh";
#endif
        using var process = new Process {
            StartInfo = new ProcessStartInfo(executable) {
                Arguments = "-NoLogo -NoProfile -NonInteractive -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000)) {
            process.Kill();
            process.WaitForExit();
            throw new TimeoutException("Owned PowerShell command proof exceeded 30 seconds.");
        }
        string diagnostics = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, diagnostics);
        Assert.Contains("COMMAND_PROOF_PASSED", diagnostics);
    }
}
