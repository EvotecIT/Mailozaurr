using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
#if !UNIX
using System.Security.AccessControl;
using System.Security.Principal;
#endif
using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests;

public class EphemeralOpenPgpContextTests
{
    [Fact]
    public async Task CreateTempDirectories_AreUniqueAcrossThreads()
    {
        const int count = 20;
        var bag = new ConcurrentBag<string>();
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(() =>
        {
            using var ctx = new EphemeralOpenPgpContext();
            var dir = (string)field.GetValue(ctx)!;
            bag.Add(dir);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(count, bag.Distinct(StringComparer.Ordinal).Count());
        foreach (var dir in bag)
        {
            Assert.False(Directory.Exists(dir));
        }
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenDirectoryAlreadyDeleted()
    {
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ctx = new EphemeralOpenPgpContext();
        var dir = (string)field.GetValue(ctx)!;

        Assert.True(Directory.Exists(dir));
        Directory.Delete(dir, true);
        Assert.False(Directory.Exists(dir));

        var ex = Record.Exception(() => ctx.Dispose());
        Assert.Null(ex);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void CreateTempDirectory_HasRestrictivePermissions()
    {
        var field = typeof(EphemeralOpenPgpContext).GetField("_tempDirectory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var ctx = new EphemeralOpenPgpContext();
        var dir = (string)field.GetValue(ctx)!;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
#if !UNIX
            var security = new DirectoryInfo(dir).GetAccessControl();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            var current = WindowsIdentity.GetCurrent().User;
            bool hasUserRule = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference.Equals(current) && rule.AccessControlType == AccessControlType.Allow && rule.FileSystemRights.HasFlag(FileSystemRights.FullControl))
                {
                    hasUserRule = true;
                }
                else if (rule.AccessControlType == AccessControlType.Allow)
                {
                    Assert.False(true, "Unexpected access rule found");
                }
            }
            Assert.True(hasUserRule);
#endif
        }
        else
        {
            var mode = new DirectoryInfo(dir).UnixFileMode;
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, mode);
        }
    }
#endif
}
