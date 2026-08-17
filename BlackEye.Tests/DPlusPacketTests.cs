namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using System;
    using Xunit;

    /// <summary>
    /// The DPlus reader keeps the whole datagram, so these offsets match
    /// DPlusProtocol.md exactly.
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
        public void FramePayloadStartsAtSeventeen()
        {
            // Byte 16 is the packet id; the 12 byte payload runs from 17 to 28.
            var frame = new DPlusFramePacket(CaptureBytes.DPlusFrame);

            Assert.Equal(9, frame.Ambe.Length);
            Assert.Equal(3, frame.Data.Length);
            Assert.Equal(12, frame.AmbeAndData.Length);
            Assert.Equal(new byte[] { 0x5b, 0x61, 0x94, 0x4b, 0xd4, 0xe3, 0xe0, 0xa0, 0x6a }, frame.Ambe);
            Assert.Equal(new byte[] { 0x55, 0x55, 0x55 }, frame.Data);
        }

        [Fact]
        public void FrameAmbeAndDataIsTheConcatenationOfItsTwoHalves()
        {
            var frame = new DPlusFramePacket(CaptureBytes.DPlusVoiceFrame(0x0a));

            Assert.Equal(frame.Ambe, frame.AmbeAndData[0..9]);
            Assert.Equal(frame.Data, frame.AmbeAndData[9..12]);
        }

        [Fact]
        public void TheEndOfTransmissionFramesTrailingBytesAreNotPayload()
        {
            // This frame is 32 bytes: an open ended range would swallow the three
            // trailing bytes and break both the payload and IsLast().
            var eot = new DPlusFramePacket(CaptureBytes.DPlusFrameEot);

            Assert.Equal(12, eot.AmbeAndData.Length);
            Assert.Equal(3, eot.Data.Length);
            Assert.Equal(CaptureBytes.SilentAmbe, eot.Ambe);
        }

        [Fact]
        public void TheLastVoiceFrameOfAStreamIsRecognised()
        {
            Assert.True(new DPlusFramePacket(CaptureBytes.DPlusFrame).IsLast());
        }

        [Fact]
        public void TheEndOfTransmissionFrameIsRecognisedAsLast()
        {
            Assert.True(new DPlusFramePacket(CaptureBytes.DPlusFrameEot).IsLast());
        }

        [Fact]
        public void OrdinaryVoiceFramesAreNotLast()
        {
            Assert.False(new DPlusFramePacket(CaptureBytes.DPlusVoiceFrame(0x00)).IsLast());
            Assert.False(new DPlusFramePacket(CaptureBytes.DPlusVoiceFrame(0x14)).IsLast());
        }

        [Fact]
        public void APayloadReadOffTheWireCanBeWrittenBackUnchanged()
        {
            var frame = new DPlusFramePacket(CaptureBytes.DPlusFrame);

            var rewritten = new DPlusNetworkWriter().WriteFrame(frame.AmbeAndData, (short)0x7D37, 0x11);

            Assert.Equal(CaptureBytes.DPlusFrame, rewritten);
        }

        [Fact]
        public void APayloadReadOffTheWireIsAcceptedByTheIcomWriter()
        {
            // This is what the bridge actually does with it, and it throws unless
            // the payload is exactly 12 bytes.
            var frame = new DPlusFramePacket(CaptureBytes.DPlusVoiceFrame(0x03));

            var terminalFrame = new BlackEye.Connectivity.IcomTerminal.IcomTerminalWriter()
                .WriteFrame(sequenceId: 0x03, number: 0x03, ambeAndData: frame.AmbeAndData);

            Assert.Equal(17, terminalFrame.Length);
            Assert.Equal(frame.AmbeAndData, terminalFrame[4..16]);
        }

        [Fact]
        public void LoginAckDistinguishesOkrwFromBusy()
        {
            Assert.True(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginAck).Ack);
            Assert.False(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginNak).Ack);
        }
    }
}
