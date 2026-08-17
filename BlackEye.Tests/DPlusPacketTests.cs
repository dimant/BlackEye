namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using System;
    using Xunit;

    /// <summary>
    /// The DPlus reader keeps the whole datagram, so these offsets match
    /// DPlusProtocol.md exactly.
    ///
    /// Frame payload offsets are deliberately not asserted here: they are wrong in
    /// the current code (finding F04) and Task 4 of the conformance plan adds the
    /// tests that drive the fix.
    /// </summary>
    public class DPlusPacketTests
    {
        [Fact]
        public void NullBufferIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DPlusHeaderPacket(null!));
            Assert.Throws<ArgumentNullException>(() => new DPlusFramePacket(null!));
        }

        [Fact]
        public void LengthIsTheFirstByteAndCountsItself()
        {
            Assert.Equal(58, new DPlusHeaderPacket(CaptureBytes.DPlusHeader).Length);
            Assert.Equal(29, new DPlusFramePacket(CaptureBytes.DPlusFrame).Length);
            Assert.Equal(32, new DPlusFramePacket(CaptureBytes.DPlusFrameEot).Length);

            Assert.Equal(CaptureBytes.DPlusHeader.Length, new DPlusHeaderPacket(CaptureBytes.DPlusHeader).Length);
        }

        [Fact]
        public void HeaderCallsignOffsetsMatchTheCapture()
        {
            var header = new DPlusHeaderPacket(CaptureBytes.DPlusHeader);

            Assert.Equal("REF030 C", header.Rpt2);
            Assert.Equal("AI6VW  D", header.Rpt1);
            Assert.Equal("CQCQCQ  ", header.UrCall);
            Assert.Equal("AI6VW   ", header.MyCall);
            Assert.Equal("ID52", header.Suffix);
        }

        [Fact]
        public void HeaderRepeaterFieldsSitInTheOppositeOrderToTheIcomProtocol()
        {
            // This is the single easiest thing to get wrong when bridging: Rpt2 is
            // first on the wire in DPlus, Rpt1 is first in Icom terminal mode.
            var dplus = new DPlusHeaderPacket(CaptureBytes.DPlusHeader);

            Assert.Equal("REF030 C", System.Text.Encoding.UTF8.GetString(CaptureBytes.DPlusHeader[20..28]));
            Assert.Equal(dplus.Rpt2, System.Text.Encoding.UTF8.GetString(CaptureBytes.DPlusHeader[20..28]));
            Assert.Equal(dplus.Rpt1, System.Text.Encoding.UTF8.GetString(CaptureBytes.DPlusHeader[28..36]));
        }

        [Fact]
        public void LoginAckDistinguishesOkrwFromBusy()
        {
            Assert.True(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginAck).Ack);
            Assert.False(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginNak).Ack);
        }
    }
}
