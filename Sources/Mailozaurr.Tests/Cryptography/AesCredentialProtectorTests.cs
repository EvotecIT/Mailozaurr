using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Mailozaurr;

namespace Mailozaurr.Tests.Cryptography;

public sealed class AesCredentialProtectorTests {
    [Fact]
    public async Task ConcurrentInstancesShareKeyMaterial() {
        using var scope = new KeyDirectoryScope();

        var keyDirectory = CredentialProtectionPaths.ResolveKeyDirectory();
        if (Directory.Exists(keyDirectory)) {
            Directory.Delete(keyDirectory, true);
        }

        var startEvent = new ManualResetEventSlim(false);
        var concurrencyLevel = Math.Max(4, Environment.ProcessorCount);
        var tasks = new List<Task<(byte[] Key, string CipherText, string PlainText)>>(concurrencyLevel);

        for (var i = 0; i < concurrencyLevel; i++) {
            var secret = $"secret-{i}";
            tasks.Add(Task.Run(() => {
                startEvent.Wait();
                var protector = new AesCredentialProtector();
                var cipher = protector.Protect(secret);
                var keyMaterial = GetKeyMaterial(protector);
                return (keyMaterial, cipher, secret);
            }));
        }

        startEvent.Set();

        var results = await Task.WhenAll(tasks);

        var verifier = new AesCredentialProtector();
        foreach (var (_, cipher, plain) in results) {
            var roundtrip = verifier.Unprotect(cipher);
            Assert.Equal(plain, roundtrip);
        }

        var keyPath = Path.Combine(keyDirectory, "credential.key");
        Assert.True(File.Exists(keyPath));
        var keyBytes = File.ReadAllBytes(keyPath);
        Assert.Equal(32, keyBytes.Length);

        foreach (var (key, _, _) in results) {
            Assert.Equal(keyBytes, key);
        }
    }

    private static byte[] GetKeyMaterial(AesCredentialProtector protector) {
        var field = typeof(AesCredentialProtector).GetField("key", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(protector) as byte[];
        Assert.NotNull(value);
        return ((byte[])value!).ToArray();
    }

    private sealed class KeyDirectoryScope : IDisposable {
        private const string OverrideVariable = "MAILOZAURR_KEY_DIRECTORY";

        private static readonly string[] BasePathVariables = new[] {
            "LOCALAPPDATA",
            "APPDATA",
            "HOME",
            "USERPROFILE",
            "XDG_DATA_HOME"
        };

        private readonly Dictionary<string, string?> previousValues = new();
        private readonly string root;

        public KeyDirectoryScope() {
            root = Path.Combine(Path.GetTempPath(), "Mailozaurr", "KeyTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            SetVariable(OverrideVariable, Path.Combine(root, "keys"));

            foreach (var variable in BasePathVariables) {
                SetVariable(variable, root);
            }
        }

        public void Dispose() {
            foreach (var pair in previousValues) {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            try {
                if (Directory.Exists(root)) {
                    Directory.Delete(root, true);
                }
            } catch {
                // Best-effort cleanup.
            }
        }

        private void SetVariable(string name, string value) {
            previousValues[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
