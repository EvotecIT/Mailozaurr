using Mailozaurr.Application;
using System.Diagnostics;

namespace Mailozaurr.Tests;

public sealed class ApplicationJsonFileDocumentStoreTests {
    private const string WorkerFileVariable = "MAILOZAURR_JSON_WORKER_FILE";
    private const string WorkerGateVariable = "MAILOZAURR_JSON_WORKER_GATE";
    private const string WorkerReadyVariable = "MAILOZAURR_JSON_WORKER_READY";
    private const string WorkerProfileVariable = "MAILOZAURR_JSON_WORKER_PROFILE";

    [Fact]
    public async Task WriterLockWaitHasAFiniteTimeout() {
        string filePath = CreateTemporaryFilePath();
        string lockDirectory = Path.Combine(Path.GetDirectoryName(filePath)!, ".mailozaurr-locks");
        string lockPath = Path.Combine(lockDirectory, Path.GetFileName(filePath) + ".lock");
        Directory.CreateDirectory(lockDirectory);
        using var heldLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var store = new JsonFileDocumentStore<MailProfileStoreDocument>(
            filePath,
            "Profile store path is invalid.",
            ApplicationJsonContext.Default.MailProfileStoreDocument,
            static () => new MailProfileStoreDocument(),
            lockTimeout: TimeSpan.FromMilliseconds(150));

        TimeoutException exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            store.UpdateAsync(document => document.Profiles.Add(new MailProfile {
                Id = "blocked",
                DisplayName = "Blocked",
                Kind = MailProfileKind.Imap
            })));

        Assert.Contains("Timed out", exception.Message, StringComparison.Ordinal);
        Assert.IsType<IOException>(exception.InnerException);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task IndependentProcessesDoNotLoseConcurrentProfileWrites() {
        const int workerCount = 8;
        string root = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(root, "profiles.json");
        string gatePath = Path.Combine(root, "start.gate");
        string readyDirectory = Path.Combine(root, "ready");
        Directory.CreateDirectory(readyDirectory);
        var workers = new List<WorkerProcess>();

        try {
            for (var index = 0; index < workerCount; index++) {
                workers.Add(StartWorker(filePath, gatePath, readyDirectory, $"profile-{index}"));
            }

            DateTime readyDeadline = DateTime.UtcNow.AddSeconds(20);
            while (Directory.GetFiles(readyDirectory).Length < workerCount && DateTime.UtcNow < readyDeadline) {
                await Task.Delay(25);
            }
            int readyCount = Directory.GetFiles(readyDirectory).Length;
            File.WriteAllText(gatePath, "go");

            using var waitTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.WhenAll(workers.Select(worker =>
                worker.Process.WaitForExitAsync(waitTimeout.Token)));
            string diagnostics = await BuildWorkerDiagnosticsAsync(workers);

            Assert.True(readyCount == workerCount,
                $"Only {readyCount} of {workerCount} workers reached the contention gate.{Environment.NewLine}{diagnostics}");
            Assert.True(workers.All(worker => worker.Process.ExitCode == 0), diagnostics);

            IReadOnlyList<MailProfile> profiles = await new FileMailProfileStore(filePath).GetAllAsync();
            Assert.Equal(workerCount, profiles.Count);
            Assert.Equal(
                workerCount,
                profiles.Select(profile => profile.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        } finally {
            foreach (WorkerProcess worker in workers) {
                if (!worker.Process.HasExited) {
                    worker.Process.Kill(entireProcessTree: true);
                    worker.Process.WaitForExit(5000);
                }
                worker.Process.Dispose();
            }
            if (Directory.Exists(root)) {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact(Explicit = true)]
    public async Task CrossProcessProfileWriteWorker() {
        string filePath = RequireWorkerVariable(WorkerFileVariable);
        string gatePath = RequireWorkerVariable(WorkerGateVariable);
        string readyPath = RequireWorkerVariable(WorkerReadyVariable);
        string profileId = RequireWorkerVariable(WorkerProfileVariable);
        File.WriteAllText(readyPath, "ready");
        await WaitForFileAsync(gatePath, TimeSpan.FromSeconds(20));

        await new FileMailProfileStore(filePath).SaveAsync(new MailProfile {
            Id = profileId,
            DisplayName = profileId,
            Kind = MailProfileKind.Imap
        });
    }

    private static WorkerProcess StartWorker(
        string filePath,
        string gatePath,
        string readyDirectory,
        string profileId) {
        string testAssemblyPath = typeof(ApplicationJsonFileDocumentStoreTests).Assembly.Location;
        string dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var startInfo = new ProcessStartInfo(dotnetHost) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(testAssemblyPath);
        startInfo.ArgumentList.Add("-noLogo");
        startInfo.ArgumentList.Add("-noColor");
        startInfo.ArgumentList.Add("-reporter");
        startInfo.ArgumentList.Add("silent");
        startInfo.ArgumentList.Add("-parallel");
        startInfo.ArgumentList.Add("none");
        startInfo.ArgumentList.Add("-explicit");
        startInfo.ArgumentList.Add("only");
        startInfo.ArgumentList.Add("-method");
        startInfo.ArgumentList.Add(
            "Mailozaurr.Tests.ApplicationJsonFileDocumentStoreTests.CrossProcessProfileWriteWorker");
        startInfo.Environment[WorkerFileVariable] = filePath;
        startInfo.Environment[WorkerGateVariable] = gatePath;
        startInfo.Environment[WorkerReadyVariable] = Path.Combine(readyDirectory, profileId + ".ready");
        startInfo.Environment[WorkerProfileVariable] = profileId;

        Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The cross-process JSON worker could not be started.");
        return new WorkerProcess(
            process,
            process.StandardOutput.ReadToEndAsync(),
            process.StandardError.ReadToEndAsync());
    }

    private static async Task<string> BuildWorkerDiagnosticsAsync(IEnumerable<WorkerProcess> workers) {
        var diagnostics = new List<string>();
        foreach (WorkerProcess worker in workers) {
            string output = await worker.StandardOutput;
            string error = await worker.StandardError;
            diagnostics.Add(
                $"pid={worker.Process.Id}; exit={worker.Process.ExitCode}; stdout={output}; stderr={error}");
        }
        return string.Join(Environment.NewLine, diagnostics);
    }

    private static async Task WaitForFileAsync(string path, TimeSpan timeout) {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (!File.Exists(path)) {
            if (DateTime.UtcNow >= deadline) {
                throw new TimeoutException($"Timed out waiting for cross-process gate '{path}'.");
            }
            await Task.Delay(25);
        }
    }

    private static string RequireWorkerVariable(string name) {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Required worker variable '{name}' was not provided.")
            : value;
    }

    private sealed class WorkerProcess {
        internal WorkerProcess(
            Process process,
            Task<string> standardOutput,
            Task<string> standardError) {
            Process = process;
            StandardOutput = standardOutput;
            StandardError = standardError;
        }

        internal Process Process { get; }
        internal Task<string> StandardOutput { get; }
        internal Task<string> StandardError { get; }
    }
#endif

    private static string CreateTemporaryFilePath() {
        string directory = Path.Combine(Path.GetTempPath(), "Mailozaurr.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "profiles.json");
    }
}