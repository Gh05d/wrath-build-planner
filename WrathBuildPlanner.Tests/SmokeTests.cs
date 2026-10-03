using Xunit;

namespace WrathBuildPlanner.Tests {
    public class SmokeTests {
        [Fact]
        public void TestHostRuns() {
            Assert.Equal(4, 2 + 2);
        }
    }
}
