using MailKit.Security;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAuthenticationMechanismTests {
    private class FakeClient : ClientSmtp {
        public SaslMechanism? Mechanism;
        public override void Authenticate(SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken = default) {
            Mechanism = mechanism;
        }
    }

    [Fact]
    public void Authenticate_UsesSelectedMechanism() {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);

        smtp.Authenticate("user", "pass", false, AuthenticationMechanism.CramMd5);

        Assert.IsType<SaslMechanismCramMd5>(fake.Mechanism);
    }
}