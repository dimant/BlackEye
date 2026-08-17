namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using System;
    using Xunit;

    /// <summary>
    /// Covers the DPlus writer against the captures.
    ///
    /// WriteLogin and WriteFrameEot are deliberately absent: both are wrong today
    /// (findings F02, F03) and Tasks 2-3 of the conformance plan add the failing
    /// tests that drive those fixes.
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
        public void HeaderMatchesTheCaptureThroughTheCallsigns()
        {
            var header = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x7D37);

            Assert.Equal(58, header.Length);

            // Bytes 56..57 are the CRC and are covered by Task 5.
            Assert.Equal(CaptureBytes.DPlusHeader[0..56], header[0..56]);
        }

        [Fact]
        public void HeaderLengthByteCountsItself()
        {
            var header = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x0001);

            Assert.Equal(0x3a, header[0]);
            Assert.Equal(58, header.Length);
        }

        [Fact]
        public void HeaderPlacesEachCallsignAtItsDocumentedOffset()
        {
            var header = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x0001);

            Assert.Equal("REF030 C", System.Text.Encoding.UTF8.GetString(header[20..28]));
            Assert.Equal("AI6VW  D", System.Text.Encoding.UTF8.GetString(header[28..36]));
            Assert.Equal("CQCQCQ  ", System.Text.Encoding.UTF8.GetString(header[36..44]));
            Assert.Equal("AI6VW   ", System.Text.Encoding.UTF8.GetString(header[44..52]));
            Assert.Equal("ID52", System.Text.Encoding.UTF8.GetString(header[52..56]));
        }

        [Fact]
        public void HeaderCarriesTheDsvtSignatureAndTheSessionId()
        {
            var header = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x7D37);

            Assert.Equal(0x80, header[1]);
            Assert.Equal("DSVT", System.Text.Encoding.UTF8.GetString(header[2..6]));

            // Byte 6 is 0x10 for a header, against 0x20 for a frame.
            Assert.Equal(0x10, header[6]);
            Assert.Equal(0x7d, header[14]);
            Assert.Equal(0x37, header[15]);
            Assert.Equal(0x80, header[16]);
        }

        [Fact]
        public void HeaderFromARawBlobMatchesTheStringOverload()
        {
            // The blob is ordered Rpt2 first, which is the DPlus wire order and the
            // opposite of the Icom writer's equivalent overload.
            var blob = new byte[36];
            System.Text.Encoding.UTF8.GetBytes("REF030 C").CopyTo(blob, 0);
            System.Text.Encoding.UTF8.GetBytes("AI6VW  D").CopyTo(blob, 8);
            System.Text.Encoding.UTF8.GetBytes("CQCQCQ  ").CopyTo(blob, 16);
            System.Text.Encoding.UTF8.GetBytes("AI6VW   ").CopyTo(blob, 24);
            System.Text.Encoding.UTF8.GetBytes("ID52").CopyTo(blob, 32);

            var fromStrings = writer.WriteHeader(
                rpt1: "AI6VW  D",
                rpt2: "REF030 C",
                urcall: "CQCQCQ  ",
                mycall: "AI6VW   ",
                suffix: "ID52",
                sessionid: 0x7D37);

            Assert.Equal(fromStrings, writer.WriteHeader(blob, 0x7D, 0x37));
        }

        [Fact]
        public void HeaderRejectsABlobThatIsNotThirtySixBytes()
        {
            Assert.Throws<ArgumentException>(() => writer.WriteHeader(new byte[35], 0x00, 0x01));
            Assert.Throws<ArgumentException>(() => writer.WriteHeader(new byte[37], 0x00, 0x01));
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
