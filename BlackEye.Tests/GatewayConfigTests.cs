namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using Xunit;

    public class GatewayConfigTests
    {
        [Fact]
        public void RepeaterFieldsAreEightCharactersWithTheModuleLast()
        {
            var config = new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');

            Assert.Equal("AI6VW  D", config.Rpt1);
            Assert.Equal("REF030 C", config.Rpt2);
            Assert.Equal("AI6VW   ", config.PaddedMyCall);
            Assert.Equal("CQCQCQ  ", config.UrCall);
            Assert.Equal("ID52", config.PaddedSuffix);
        }

        [Fact]
        public void EveryFieldIsTheWidthTheProtocolsExpect()
        {
            var config = new GatewayConfig("W1AW", "HT", "XLX999", 'B', 'A');

            Assert.Equal(8, config.Rpt1.Length);
            Assert.Equal(8, config.Rpt2.Length);
            Assert.Equal(8, config.PaddedMyCall.Length);
            Assert.Equal(8, config.UrCall.Length);
            Assert.Equal(4, config.PaddedSuffix.Length);
        }

        [Fact]
        public void TheDefaultMatchesTheCapturedSession()
        {
            Assert.Equal("AI6VW  D", GatewayConfig.Default.Rpt1);
            Assert.Equal("REF030 C", GatewayConfig.Default.Rpt2);
        }
    }
}
