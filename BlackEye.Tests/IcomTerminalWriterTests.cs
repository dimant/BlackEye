namespace BlackEye.Tests
{
    using BlackEye.Connectivity.IcomTerminal;
    using System;
    using Xunit;

    /// <summary>
    /// Writers hold no state: every method is a pure byte-layout function, which is
    /// exactly what can be checked against the captures.
    ///
    /// Callsign arguments are passed by name throughout, so these tests keep
    /// asserting the right field even if the parameter order is reworked.
    /// </summary>
    public class IcomTerminalWriterTests
    {
        private readonly IcomTerminalWriter writer = new IcomTerminalWriter();

        [Fact]
        public void PingMatchesTheCapture()
        {
            Assert.Equal(CaptureBytes.IcomPingWire, writer.WritePing());
        }

        [Fact]
        public void ResetIsNothingButTerminatorBytes()
        {
            var reset = writer.WriteReset();

            Assert.NotEmpty(reset);
            Assert.All(reset, b => Assert.Equal(0xFF, b));
        }

        [Fact]
        public void HeaderMatchesTheCapture()
        {
            var header = writer.WriteHeader(
                rpt1: "AI6VW  L",
                rpt2: "AI6VW  G",
                urcall: "AI6VW   ",
                mycall: "AI6VW  G",
                suffix: "    ");

            Assert.Equal(CaptureBytes.IcomHeaderToRadioWire, header);
        }

        [Fact]
        public void HeaderIsFortyOneBytesPlusTerminatorAndCarriesNoCrc()
        {
            var header = writer.WriteHeader(
                rpt1: "AA1BBC C",
                rpt2: "BB2DDE A",
                urcall: "CQCQCQ  ",
                mycall: "YZ1AB   ",
                suffix: "ID52");

            // Length counts from byte 1 onward, so 0x29 bytes follow the length byte
            // and the packet occupies indices 0..41.
            Assert.Equal(42, header.Length);
            Assert.Equal(0x29, header[0]);
            Assert.Equal(0x20, header[1]);
            Assert.Equal(0xFF, header[41]);
        }

        [Fact]
        public void HeaderPlacesEachCallsignAtItsDocumentedOffset()
        {
            var header = writer.WriteHeader(
                rpt1: "AA1BBC C",
                rpt2: "BB2DDE A",
                urcall: "CQCQCQ  ",
                mycall: "YZ1AB   ",
                suffix: "ID52");

            Assert.Equal("AA1BBC C", System.Text.Encoding.UTF8.GetString(header[5..13]));
            Assert.Equal("BB2DDE A", System.Text.Encoding.UTF8.GetString(header[13..21]));
            Assert.Equal("CQCQCQ  ", System.Text.Encoding.UTF8.GetString(header[21..29]));
            Assert.Equal("YZ1AB   ", System.Text.Encoding.UTF8.GetString(header[29..37]));
            Assert.Equal("ID52", System.Text.Encoding.UTF8.GetString(header[37..41]));
        }

        [Fact]
        public void HeaderFromARawBlobMatchesTheStringOverload()
        {
            var blob = new byte[36];
            System.Text.Encoding.UTF8.GetBytes("AA1BBC C").CopyTo(blob, 0);
            System.Text.Encoding.UTF8.GetBytes("BB2DDE A").CopyTo(blob, 8);
            System.Text.Encoding.UTF8.GetBytes("CQCQCQ  ").CopyTo(blob, 16);
            System.Text.Encoding.UTF8.GetBytes("YZ1AB   ").CopyTo(blob, 24);
            System.Text.Encoding.UTF8.GetBytes("ID52").CopyTo(blob, 32);

            var fromStrings = writer.WriteHeader(
                rpt1: "AA1BBC C",
                rpt2: "BB2DDE A",
                urcall: "CQCQCQ  ",
                mycall: "YZ1AB   ",
                suffix: "ID52");

            Assert.Equal(fromStrings, writer.WriteHeader(blob));
        }

        [Fact]
        public void FrameCarriesTheIdsAndTheTwelveBytePayload()
        {
            var payload = new byte[]
            {
                0xb2, 0x4d, 0x22, 0x48, 0xc0, 0x16, 0x28, 0x26, 0xc8,
                0x55, 0x2d, 0x16
            };

            var frame = writer.WriteFrame(sequenceId: 0x00, number: 0x00, ambeAndData: payload);

            Assert.Equal(CaptureBytes.IcomFrameFromRadioWire.Length, frame.Length);
            Assert.Equal(0x10, frame[0]);
            Assert.Equal(0x22, frame[1]);
            Assert.Equal(0x00, frame[2]);
            Assert.Equal(0x00, frame[3]);
            Assert.Equal(payload, frame[4..16]);
            Assert.Equal(0xFF, frame[16]);
        }

        [Fact]
        public void FrameIdsAreWrittenWhereTheRadioExpectsThem()
        {
            var frame = writer.WriteFrame(sequenceId: 0x2a, number: 0x0b, ambeAndData: new byte[12]);

            Assert.Equal(0x2a, frame[2]);
            Assert.Equal(0x0b, frame[3]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(9)]
        [InlineData(11)]
        [InlineData(13)]
        public void FrameRejectsAPayloadThatIsNotTwelveBytes(int length)
        {
            Assert.Throws<ArgumentException>(() => writer.WriteFrame(0x00, 0x00, new byte[length]));
        }

        [Fact]
        public void EveryFrameToTheRadioIsSeventeenBytesAndTerminated()
        {
            var frames = new[]
            {
                writer.WriteFrame(0x01, 0x01, new byte[12]),
                writer.WriteFrameEot(),
                writer.WriteEmptyVoiceEmptyData(),
                writer.WriteEmptyVoiceSyncData(),
                writer.WriteEmptyVoiceLastFrame(),
            };

            Assert.All(frames, frame =>
            {
                Assert.Equal(17, frame.Length);
                Assert.Equal(0x10, frame[0]);
                Assert.Equal(0x22, frame[1]);
                Assert.Equal(0xFF, frame[16]);
            });
        }

        [Fact]
        public void EndOfTransmissionFrameCarriesTheDocumentedPayload()
        {
            var eot = writer.WriteFrameEot();

            // 55 c8 7a then nine 0x55: "last frame without voice".
            var expected = new byte[]
            {
                0x55, 0xc8, 0x7a,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55
            };

            Assert.Equal(expected, eot[4..16]);
        }

        [Fact]
        public void EndOfTransmissionFrameMarksItselfAsLast()
        {
            var eot = writer.WriteFrameEot();

            Assert.Equal(0x40, eot[3] & 0x40);
        }

        [Fact]
        public void EmptyVoiceFramesShareTheSilentAmbePayload()
        {
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceEmptyData()[4..13]);
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceSyncData()[4..13]);
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceLastFrame()[4..13]);
        }

        [Fact]
        public void SyncDataFrameCarriesTheSlowDataSyncPattern()
        {
            Assert.Equal(new byte[] { 0x55, 0x2d, 0x16 }, writer.WriteEmptyVoiceSyncData()[13..16]);
        }

        [Fact]
        public void LastFrameCarriesTheEndOfStreamSlowData()
        {
            Assert.Equal(new byte[] { 0x55, 0x55, 0x55 }, writer.WriteEmptyVoiceLastFrame()[13..16]);
        }
    }
}
