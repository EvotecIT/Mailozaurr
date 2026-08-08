using Mailozaurr.Definitions;
using System.IO;
using Xunit;

namespace Mailozaurr.Tests;

public class AttachmentPathIdentityTests {
    [Fact]
    public void CreateSet_UsesPlatformPathCaseSemantics() {
        var paths = AttachmentPathIdentity.CreateSet();

        Assert.True(paths.Add("attachment.tmp"));
        Assert.Equal(Path.DirectorySeparatorChar != '\\', paths.Add("ATTACHMENT.TMP"));
    }

    [Fact]
    public void Add_CanonicalizesRelativeAndAbsolutePaths() {
        var fileName = $"attachment-{System.Guid.NewGuid():N}.tmp";
        var absolutePath = Path.Combine(System.Environment.CurrentDirectory, fileName);
        var paths = AttachmentPathIdentity.CreateSet();

        Assert.True(AttachmentPathIdentity.Add(paths, fileName));
        Assert.False(AttachmentPathIdentity.Add(paths, absolutePath));
    }
}
