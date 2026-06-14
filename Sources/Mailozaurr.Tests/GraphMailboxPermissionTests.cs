using System.Collections.Generic;
using Xunit;

namespace Mailozaurr.Tests;

public class GraphMailboxPermissionTests {
    [Fact]
    public void Constructor_ParsesRoles_IgnoringCase() {
        var raw = new Dictionary<string, object> {
            ["roles"] = new object[] { "owner", "read", "write" }
        };
        var perm = new Mailozaurr.GraphMailboxPermission(raw);
        Assert.NotNull(perm.Roles);
        Assert.Contains(GraphMailboxRole.Owner, perm.Roles!);
        Assert.Contains(GraphMailboxRole.Read, perm.Roles!);
        Assert.Contains(GraphMailboxRole.Write, perm.Roles!);
    }
}