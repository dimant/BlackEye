namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using Xunit;

    /// <summary>
    /// Covers the DPlus writer methods that already agree with the captures.
    ///
    /// WriteLogin, WriteHeader and WriteFrameEot are deliberately absent: all three
    /// are wrong today (findings F01, F02, F03) and Tasks 1-3 of the conformance
    /// plan add the failing tests that drive those fixes.
    /// </summary>
    public class DPlusNetworkWriterTests
    {
        private readonly DPlusNetworkWriter writer = new DPlusNetworkWriter();

        [Fact]
        public void PingMatchesTheCapture()
        {
            Assert.Equal(CaptureBytes.DPlusPing, writer.WritePing());
        }

        [Fact]
        public void ConnectMatchesTheCapture()
        {
            Assert.Equal(CaptureBytes.DPlusConnect, writer.WriteConnect());
        }

        [Fact]
        public void DisconnectDiffersFromConnectOnlyInTheLastByte()
        {
            var connect = writer.WriteConnect();
            var disconnect = writer.WriteDisconnect();

            Assert.Equal(CaptureBytes.DPlusDisconnect, disconnect);
            Assert.Equal(connect[0..4], disconnect[0..4]);
            Assert.Equal(0x01, connect[4]);
            Assert.Equal(0x00, disconnect[4]);
        }

        [Fact]
        public void FrameMatchesTheCapture()
        {
            var payload = CaptureBytes.DPlusFrame[17..29];

            var frame = writer.WriteFrame(payload, (short)0x7D37, 0x11);

            Assert.Equal(CaptureBytes.DPlusFrame, frame);
        }

        [Fact]
        public void FrameLengthByteCountsItself()
        {
            var frame = writer.WriteFrame(new byte[12], (short)0x0001, 0x00);

            Assert.Equal(29, frame.Length);
            Assert.Equal(29, frame[0]);
        }

        [Fact]
        public void FrameSessionIdIsBigEndianAndPacketIdFollowsIt()
        {
            var frame = writer.WriteFrame(new byte[12], (short)0x7D37, 0x11);

            Assert.Equal(0x7d, frame[14]);
            Assert.Equal(0x37, frame[15]);
            Assert.Equal(0x11, frame[16]);
        }

        [Fact]
        public void FrameCarriesTheDsvtSignature()
        {
            var frame = writer.WriteFrame(new byte[12], (short)0x0001, 0x00);

            Assert.Equal(0x80, frame[1]);
            Assert.Equal("DSVT", System.Text.Encoding.UTF8.GetString(frame[2..6]));
            Assert.Equal(0x20, frame[6]);
        }

        [Fact]
        public void SplitAmbeAndDataOverloadAssemblesTheSamePayload()
        {
            var ambe = CaptureBytes.DPlusFrame[17..26];
            var data = CaptureBytes.DPlusFrame[26..29];

            var fromSplit = writer.WriteFrame(ambe, data, (short)0x7D37, 0x11);
            var fromCombined = writer.WriteFrame(CaptureBytes.DPlusFrame[17..29], (short)0x7D37, 0x11);

            Assert.Equal(fromCombined, fromSplit);
            Assert.Equal(CaptureBytes.DPlusFrame, fromSplit);
        }

        [Fact]
        public void ExplicitSessionIdBytesOverloadMatchesTheShortOverload()
        {
            var payload = new byte[12];

            var fromShort = writer.WriteFrame(payload, (short)0x7D37, 0x05);
            var fromBytes = writer.WriteFrame(payload, (byte)0x7D, (byte)0x37, (byte)0x05);

            Assert.Equal(fromShort, fromBytes);
        }
    }
}
