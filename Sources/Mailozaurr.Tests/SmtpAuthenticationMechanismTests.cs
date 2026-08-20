using MailKit.Security;
using System.Reflection;
using Xunit;

namespace Mailozaurr.Tests;

public class SmtpAuthenticationMechanismTests {
    private class FakeClient : ClientSmtp {
        public SaslMechanism? Mechanism;
        public bool UsedAutomaticNegotiation;

        public override void Authenticate(SaslMechanism mechanism, System.Threading.CancellationToken cancellationToken = default) {
            Mechanism = mechanism;
        }

        public override void Authenticate(System.Text.Encoding encoding, System.Net.ICredentials credentials, System.Threading.CancellationToken cancellationToken = default) {
            UsedAutomaticNegotiation = true;
        }
    }

    [Fact]
    public void SendEmailMessage_DefaultsToAutomaticMechanismSelection() {
        var cmdlet = new Mailozaurr.PowerShell.CmdletSendEmailMessage();

        Assert.Equal(AuthenticationMechanism.Auto, cmdlet.AuthenticationMechanism);
    }

    [Fact]
    public void Authenticate_DefaultsToAutomaticMechanismSelection() {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);

        smtp.Authenticate("user", "pass", false);

        Assert.True(fake.UsedAutomaticNegotiation);
        Assert.Null(fake.Mechanism);
    }

    [Fact]
    public void Authenticate_UsesSelectedMechanism() {
        var smtp = new Smtp();
        var fake = new FakeClient();
        var field = typeof(Smtp).GetField("<Client>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(smtp, fake);

        smtp.Authenticate("user", "pass", false, AuthenticationMechanism.CramMd5);

        Assert.False(fake.UsedAutomaticNegotiation);
        Assert.IsType<SaslMechanismCramMd5>(fake.Mechanism);
    }
}
