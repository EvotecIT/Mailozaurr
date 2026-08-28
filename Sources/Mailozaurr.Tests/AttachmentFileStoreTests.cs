namespace Mailozaurr.Tests;

public sealed class AttachmentFileStoreTests {
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("C:\\Windows\\outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    public void Remote_names_are_contained_beneath_the_destination(string remoteName) {
        string directory = CreateTestDirectory();
        try {
            string path = AttachmentFileStore.ResolvePathInDirectory(directory, remoteName);

            Assert.Equal(directory, Path.GetDirectoryName(path));
            Assert.StartsWith("outside~", Path.GetFileName(path), StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Explicit_directory_intent_handles_a_directory_that_does_not_exist_yet() {
        string parent = CreateTestDirectory();
        string directory = Path.Combine(parent, "first-use");
        try {
            string path = MimeAttachmentStorage.ResolveDestinationPath(
                directory,
                "../report.txt",
                "attachment-1",
                AttachmentDestinationKind.Directory);

            Assert.Equal(Path.GetFullPath(directory), Path.GetDirectoryName(path));
            Assert.StartsWith("report~", Path.GetFileName(path), StringComparison.Ordinal);
        } finally {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Conflict_policies_are_atomic_and_report_the_actual_path() {
        string directory = CreateTestDirectory();
        try {
            AttachmentFileSaveResult created = AttachmentFileStore.SaveBytesToDirectory(
                directory,
                "report.txt",
                new byte[] { 1 },
                AttachmentFileConflictPolicy.Fail,
                "same");

            Assert.Equal(AttachmentFileSaveAction.Created, created.Action);
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(created.Path));
            Assert.Throws<IOException>(() => AttachmentFileStore.SaveBytesToDirectory(
                directory,
                "report.txt",
                new byte[] { 2 },
                AttachmentFileConflictPolicy.Fail,
                "same"));
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(created.Path));

            AttachmentFileSaveResult skipped = AttachmentFileStore.SaveBytesToDirectory(
                directory,
                "report.txt",
                new byte[] { 3 },
                AttachmentFileConflictPolicy.Skip,
                "same");
            Assert.Equal(AttachmentFileSaveAction.Skipped, skipped.Action);
            Assert.Equal(created.Path, skipped.Path);
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(created.Path));

            AttachmentFileSaveResult renamed = AttachmentFileStore.SaveBytesToDirectory(
                directory,
                "report.txt",
                new byte[] { 4 },
                AttachmentFileConflictPolicy.Rename,
                "same");
            Assert.Equal(AttachmentFileSaveAction.Renamed, renamed.Action);
            Assert.NotEqual(created.Path, renamed.Path);
            Assert.Equal(new byte[] { 4 }, File.ReadAllBytes(renamed.Path));

            AttachmentFileSaveResult replaced = AttachmentFileStore.SaveBytesToDirectory(
                directory,
                "report.txt",
                new byte[] { 5 },
                AttachmentFileConflictPolicy.Replace,
                "same");
            Assert.Equal(AttachmentFileSaveAction.Replaced, replaced.Action);
            Assert.Equal(created.Path, replaced.Path);
            Assert.Equal(new byte[] { 5 }, File.ReadAllBytes(created.Path));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Failed_writers_leave_no_destination_or_staging_file() {
        string directory = CreateTestDirectory();
        try {
            Assert.Throws<InvalidDataException>(() => AttachmentFileStore.SaveToDirectory(
                directory,
                "report.txt",
                stream => {
                    stream.WriteByte(1);
                    throw new InvalidDataException("broken attachment");
                }));

            Assert.Empty(Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Canceled_async_writers_leave_no_destination_or_staging_file() {
        string directory = CreateTestDirectory();
        using var cancellation = new CancellationTokenSource();
        try {
            Task<AttachmentFileSaveResult> save = AttachmentFileStore.SaveToDirectoryAsync(
                directory,
                "report.txt",
                async (stream, token) => {
                    await stream.WriteAsync(new byte[] { 1 }, 0, 1, token);
                    cancellation.Cancel();
                    await Task.Delay(Timeout.Infinite, token);
                },
                cancellationToken: cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
            Assert.Empty(Directory.GetFiles(directory));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Concurrent_rename_writers_commit_distinct_complete_files() {
        string directory = CreateTestDirectory();
        try {
            Task<AttachmentFileSaveResult>[] saves = Enumerable.Range(0, 12)
                .Select(index => Task.Run(() => AttachmentFileStore.SaveBytesToDirectory(
                    directory,
                    "report.txt",
                    new[] { (byte)index },
                    AttachmentFileConflictPolicy.Rename,
                    "shared")))
                .ToArray();

            AttachmentFileSaveResult[] results = await Task.WhenAll(saves);

            Assert.Equal(12, results.Select(result => result.Path)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(12, Directory.GetFiles(directory).Length);
            Assert.DoesNotContain(
                Directory.GetFiles(directory),
                path => Path.GetFileName(path).StartsWith(
                    ".mailozaurr-attachment-",
                    StringComparison.Ordinal));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(typeof(Mailozaurr.PowerShell.CmdletSaveGmailMessageAttachment))]
    [InlineData(typeof(Mailozaurr.PowerShell.CmdletSaveGraphMessageAttachment))]
    [InlineData(typeof(Mailozaurr.PowerShell.CmdletSaveIMAPMessageAttachment))]
    [InlineData(typeof(Mailozaurr.PowerShell.CmdletSavePOP3MessageAttachment))]
    public void Attachment_cmdlets_expose_safe_conflict_and_whatif_contracts(Type cmdletType) {
        var cmdlet = Assert.Single(cmdletType.CustomAttributes, attribute =>
            attribute.AttributeType == typeof(System.Management.Automation.CmdletAttribute));
        var supportsShouldProcess = Assert.Single(cmdlet.NamedArguments, argument =>
            argument.MemberName == nameof(System.Management.Automation.CmdletAttribute.SupportsShouldProcess));
        Assert.True(Assert.IsType<bool>(supportsShouldProcess.TypedValue.Value));
        Assert.Equal(
            typeof(AttachmentFileConflictPolicy),
            cmdletType.GetProperty("ConflictPolicy")!.PropertyType);
        Assert.Equal(
            typeof(System.Management.Automation.SwitchParameter),
            cmdletType.GetProperty("Force")!.PropertyType);
        Assert.Equal(
            typeof(System.Management.Automation.SwitchParameter),
            cmdletType.GetProperty("PassThru")!.PropertyType);
        var alias = Assert.Single(cmdletType.GetProperty("Force")!.CustomAttributes, attribute =>
            attribute.AttributeType == typeof(System.Management.Automation.AliasAttribute));
        var aliasValues = Assert.IsAssignableFrom<IReadOnlyCollection<System.Reflection.CustomAttributeTypedArgument>>(
            Assert.Single(alias.ConstructorArguments).Value);
        Assert.Contains(aliasValues, value => string.Equals(value.Value as string, "Overwrite", StringComparison.Ordinal));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Reparse_directory_ancestor_is_rejected_without_writing_through_it() {
        string root = CreateTestDirectory();
        string outside = CreateTestDirectory();
        string link = Path.Combine(root, "linked-directory");
        try {
            try {
                Directory.CreateSymbolicLink(link, outside);
            } catch (UnauthorizedAccessException) {
                return;
            } catch (PlatformNotSupportedException) {
                return;
            }

            Assert.Throws<IOException>(() => AttachmentFileStore.SaveBytesToDirectory(
                link,
                "report.txt",
                new byte[] { 9 }));
            Assert.Empty(Directory.GetFiles(outside));
        } finally {
            try {
                if (Directory.Exists(link)) Directory.Delete(link);
            } catch (IOException) {
            }
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Reparse_destination_is_rejected_without_changing_its_target() {
        string directory = CreateTestDirectory();
        try {
            string target = Path.Combine(directory, "target.txt");
            File.WriteAllText(target, "original");
            string remoteName = "linked.txt";
            string destination = AttachmentFileStore.ResolvePathInDirectory(directory, remoteName, "link");
            try {
                File.CreateSymbolicLink(destination, target);
            } catch (UnauthorizedAccessException) {
                return;
            } catch (PlatformNotSupportedException) {
                return;
            }

            Assert.Throws<IOException>(() => AttachmentFileStore.SaveBytesToDirectory(
                directory,
                remoteName,
                new byte[] { 9 },
                AttachmentFileConflictPolicy.Replace,
                "link"));
            Assert.Equal("original", File.ReadAllText(target));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
#endif

    [Fact]
    public void Mime_and_graph_legacy_helpers_use_the_shared_safe_store() {
        string directory = CreateTestDirectory();
        try {
            var mime = new MimeKit.MimePart("text", "plain") {
                FileName = "../mime.txt",
                Content = new MimeKit.MimeContent(new MemoryStream(new byte[] { 1, 2, 3 }))
            };
            var graph = new Attachment {
                Name = "..\\graph.txt",
                ContentBytes = Convert.ToBase64String(new byte[] { 4, 5, 6 })
            };

            IReadOnlyList<AttachmentFileSaveResult> mimeResults = MimeKitUtils.SaveAttachments(
                new MimeKit.MimeEntity[] { mime },
                directory,
                AttachmentFileConflictPolicy.Fail);
            IReadOnlyList<AttachmentFileSaveResult> graphResults = MicrosoftGraphUtils.SaveAttachments(
                new[] { graph },
                directory,
                AttachmentFileConflictPolicy.Fail);

            Assert.Equal(directory, Path.GetDirectoryName(Assert.Single(mimeResults).Path));
            Assert.Equal(directory, Path.GetDirectoryName(Assert.Single(graphResults).Path));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Mime_async_helper_uses_the_shared_safe_store() {
        string directory = CreateTestDirectory();
        try {
            var mime = new MimeKit.MimePart("text", "plain") {
                FileName = "../mime.txt",
                Content = new MimeKit.MimeContent(new MemoryStream(new byte[] { 1, 2, 3 }))
            };

            IReadOnlyList<AttachmentFileSaveResult> results = await MimeKitUtils.SaveAttachmentsAsync(
                new MimeKit.MimeEntity[] { mime },
                directory,
                AttachmentFileConflictPolicy.Fail,
                CancellationToken.None);

            AttachmentFileSaveResult result = Assert.Single(results);
            Assert.Equal(directory, Path.GetDirectoryName(result.Path));
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(result.Path));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTestDirectory() {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "Mailozaurr.AttachmentFileStore.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
