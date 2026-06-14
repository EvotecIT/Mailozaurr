using Xunit;

namespace Mailozaurr.Tests;

public class GraphMailboxPermissionBuilderTests {
    [Fact]
    public void Builder_CreatesPermission() {
        var perm = new GraphMailboxPermissionBuilder()
            .UserPrincipalName("owner@example.com")
            .GrantedToUser("grantee@example.com")
            .Roles(GraphMailboxRole.Owner, GraphMailboxRole.Read)
            .Build();
        Assert.Equal("owner@example.com", perm.UserPrincipalName);
        Assert.Equal("grantee@example.com", perm.GrantedTo!.User);
        Assert.Contains(GraphMailboxRole.Owner, perm.Roles!);
        Assert.Contains(GraphMailboxRole.Read, perm.Roles!);
    }
}