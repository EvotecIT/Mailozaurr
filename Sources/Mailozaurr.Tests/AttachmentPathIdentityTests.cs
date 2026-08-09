using Mailozaurr.Definitions;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Mailozaurr.Tests;

public class AttachmentPathIdentityTests {
    [Fact]
    public void CreateSet_UsesPlatformPathCaseSemantics() {
        var paths = AttachmentPathIdentity.CreateSet();

        Assert.True(paths.Add("attachment.tmp"));
        var caseSensitive = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        Assert.Equal(caseSensitive, paths.Add("ATTACHMENT.TMP"));
    }

    [Fact]
    public void Add_CanonicalizesRelativeAndAbsolutePaths() {
        var fileName = $"attachment-{System.Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(System.Environment.CurrentDirectory, fileName);
        var paths = AttachmentPathIdentity.CreateSet();

        Assert.True(AttachmentPathIdentity.Add(paths, fileName));
        Assert.False(AttachmentPathIdentity.Add(paths, absolutePath));
    }

    [Fact]
    public void Add_FollowsActualFileSystemCaseSemantics() {
        var directory = Path.Combine(Path.GetTempPath(), $"mailozaurr-case-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var lower = Path.Combine(directory, "report.txt");
        var upper = Path.Combine(directory, "REPORT.TXT");

        try {
            File.WriteAllText(lower, "lower");
            File.WriteAllText(upper, "upper");
            var distinctEntries = Directory.EnumerateFiles(directory)
                .Select(Path.GetFileName)
                .Distinct(System.StringComparer.Ordinal)
                .Count() == 2;
            var paths = AttachmentPathIdentity.CreateSet();

            Assert.True(AttachmentPathIdentity.Add(paths, lower));
            Assert.Equal(distinctEntries, AttachmentPathIdentity.Add(paths, upper));
        } finally {
            Directory.Delete(directory, recursive: true);
        }
    }
}
