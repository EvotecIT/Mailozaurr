using Xunit;

namespace Mailozaurr.Tests {
    public class RetryAlwaysTests {
        [Fact]
        public void Smtp_RetryAlways_DefaultsFalse() {
            var smtp = new Smtp();
            Assert.False(smtp.RetryAlways);
        }

        [Fact]
        public void Smtp_RetryAlways_Settable() {
            var smtp = new Smtp { RetryAlways = true };
            Assert.True(smtp.RetryAlways);
        }
    }
}