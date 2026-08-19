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
        public void ResetIsABurstOfTerminatorBytesLongEnoughToResync()
        {
            var reset = writer.WriteReset();

            // The doc calls for between 5 and 100 bytes of 0xFF.
            Assert.InRange(reset.Length, 5, 100);
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
                writer.WriteFrameEot(0x08, 0x08),
                writer.WriteEmptyVoiceEmptyData(0x00, 0x00),
                writer.WriteEmptyVoiceSyncData(0x00, 0x00),
                writer.WriteEmptyVoiceLastFrame(0x00, 0x00),
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
            var eot = writer.WriteFrameEot(0x08, 0x08);

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
            var eot = writer.WriteFrameEot(0x08, 0x08);

            Assert.Equal(0x40, eot[3] & 0x40);
        }

        [Fact]
        public void EmptyVoiceEmptyDataMatchesWhatTheReferenceAppsSend()
        {
            var frame = writer.WriteEmptyVoiceEmptyData(sequenceId: 0x00, number: 0x00);

            // 16 29 f5 is what both rs-ms3w and doozy send in this slot. The
            // 97 cb e5 quoted in IcomTerminalMode.md appears in no capture.
            Assert.Equal(CaptureBytes.IcomEmptyVoiceEmptyDataWire, frame);
        }

        [Fact]
        public void EndOfTransmissionFrameMatchesTheCapture()
        {
            // The capture shows 10 22 08 48 directly after 10 22 07 07: the ids
            // continue the transmission and 0x48 is 0x40 | 8.
            var eot = writer.WriteFrameEot(sequenceId: 0x08, number: 0x08);

            Assert.Equal(CaptureBytes.IcomFrameEotWire, eot);
        }

        [Fact]
        public void SpecialFramesCarryTheLiveTransmissionIds()
        {
            var frames = new[]
            {
                writer.WriteEmptyVoiceEmptyData(sequenceId: 0x2a, number: 0x0b),
                writer.WriteEmptyVoiceSyncData(sequenceId: 0x2a, number: 0x0b),
                writer.WriteEmptyVoiceLastFrame(sequenceId: 0x2a, number: 0x0b),
            };

            Assert.All(frames, frame =>
            {
                Assert.Equal(0x2a, frame[2]);
                Assert.Equal(0x0b, frame[3]);
            });
        }

        [Fact]
        public void TheEndOfTransmissionFrameSetsTheLastFrameBitOnAnyNumber()
        {
            for (byte number = 0; number <= 20; number++)
            {
                var eot = writer.WriteFrameEot(sequenceId: 0x11, number: number);

                Assert.Equal(0x11, eot[2]);
                Assert.Equal(0x40, eot[3] & 0x40);
                Assert.Equal(number, (byte)(eot[3] & 0x1F));
            }
        }

        [Fact]
        public void ASpecialFrameIsIndistinguishableFromAnOrdinaryOneApartFromItsPayload()
        {
            var ordinary = writer.WriteFrame(sequenceId: 0x05, number: 0x06, ambeAndData: new byte[12]);
            var filler = writer.WriteEmptyVoiceEmptyData(sequenceId: 0x05, number: 0x06);

            Assert.Equal(ordinary[0..4], filler[0..4]);
            Assert.Equal(0xFF, filler[16]);
        }

        [Fact]
        public void EmptyVoiceFramesShareTheSilentAmbePayload()
        {
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceEmptyData(0x00, 0x00)[4..13]);
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceSyncData(0x00, 0x00)[4..13]);
            Assert.Equal(CaptureBytes.SilentAmbe, writer.WriteEmptyVoiceLastFrame(0x00, 0x00)[4..13]);
        }

        [Fact]
        public void SyncDataFrameCarriesTheSlowDataSyncPattern()
        {
            Assert.Equal(new byte[] { 0x55, 0x2d, 0x16 }, writer.WriteEmptyVoiceSyncData(0x00, 0x00)[13..16]);
        }

        [Fact]
        public void LastFrameCarriesTheEndOfStreamSlowData()
        {
            Assert.Equal(new byte[] { 0x55, 0x55, 0x55 }, writer.WriteEmptyVoiceLastFrame(0x00, 0x00)[13..16]);
        }
    }
}
