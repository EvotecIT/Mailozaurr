using System.Threading.Tasks;
using Xunit;

namespace Mailozaurr.Tests {
#if NET8_0
    public class FetchExamplesTests {
        [Fact(Skip="Requires network access")]
        public async Task FetchImapExample_Runs() {
            await FetchImapMessages.RunAsync();
        }

        [Fact(Skip="Requires network access")]
        public async Task FetchPopExample_Runs() {
            await FetchPopMessages.RunAsync();
        }
    }
#endif
}
