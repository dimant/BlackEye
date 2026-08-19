namespace BlackEye.Tests
{
    using BlackEye;
    using BlackEye.Connectivity.DPlus;
    using BlackEye.Connectivity.IcomTerminal;
    using System.Linq;
    using Xunit;

    /// <summary>
    /// Drives the bridge through fake connections. Radio to network is push: every
    /// terminal frame is written to UDP as it arrives.
    /// </summary>
    public class DPlusHandlerTests
    {
        private readonly FakeConnection udp = new FakeConnection();

        private readonly FakeConnection serial = new FakeConnection();

        private readonly DPlusHandler handler;

        public DPlusHandlerTests()
        {
            handler = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp);
        }

        private static IcomTerminalFrame VoiceFrame(byte sequenceId, byte number)
        {
            var wire = new byte[]
            {
                0x10, 0x12, sequenceId, number,
                0xb2, 0x4d, 0x22, 0x48, 0xc0, 0x16, 0x28, 0x26, 0xc8,
                0x16, 0x29, 0xf5,
                0xff
            };

            return new IcomTerminalFrame(CaptureBytes.Stripped(wire));
        }

        private static IcomTerminalHeader RadioHeader() =>
            new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

        private static IcomTerminalFrame TerminatorFrame() =>
            new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomEotFrameFromRadioWire));

        private byte[][] SentOfLength(int length) =>
            udp.Sent.Where(b => b.Length == length).ToArray();

        [Fact]
        public void AHeaderFromTheRadioIsSentFiveTimes()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            // Doc step 6: headers are not acked, so send a few.
            Assert.Equal(5, SentOfLength(58).Length);
            Assert.All(SentOfLength(58), h => Assert.Equal(SentOfLength(58)[0], h));
        }

        [Fact]
        public void OutboundPacketIdCountsAndWrapsAtTwenty()
        {
            handler.TerminalListener.OnHeader(RadioHeader());
            udp.Clear();

            for (byte i = 0; i < 22; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            var packetIds = SentOfLength(29).Select(b => b[16]).ToArray();

            Assert.Equal(22, packetIds.Length);
            Assert.Equal(Enumerable.Range(0, 21).Select(i => (byte)i), packetIds[0..21]);
            Assert.Equal(0x00, packetIds[21]);
        }

        [Fact]
        public void TheHeaderIsResentEveryTwentyFrames()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            Assert.Equal(5, SentOfLength(58).Length);

            for (byte i = 0; i < 21; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            // Doc step 8: UDP drops headers, so resend once the counter wraps.
            Assert.Equal(10, SentOfLength(58).Length);

            for (byte i = 0; i < 21; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            Assert.Equal(15, SentOfLength(58).Length);
        }

        [Fact]
        public void TheResentHeaderIsTheOneThatOpenedTheTransmission()
        {
            handler.TerminalListener.OnHeader(RadioHeader());
            var opening = SentOfLength(58)[0];
            udp.Clear();

            for (byte i = 0; i < 21; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            var resent = SentOfLength(58);
            Assert.Equal(5, resent.Length);
            Assert.All(resent, h => Assert.Equal(opening, h));
        }

        [Fact]
        public void EveryFrameOfATransmissionCarriesTheSameSessionId()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            for (byte i = 0; i < 5; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            var header = SentOfLength(58)[0];
            var sessionId = header[14..16];

            Assert.All(SentOfLength(29), frame => Assert.Equal(sessionId, frame[14..16]));
        }

        [Fact]
        public void TheVoicePayloadIsPipedThroughUnchanged()
        {
            handler.TerminalListener.OnHeader(RadioHeader());
            udp.Clear();

            var frame = VoiceFrame(0x00, 0x00);
            handler.TerminalListener.OnFrame(frame);

            var sent = Assert.Single(SentOfLength(29));
            Assert.Equal(frame.AmbeAndData, sent[17..29]);
        }

        [Fact]
        public void TheRadiosTerminatorFrameBecomesADPlusEot()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            for (byte i = 0; i < 3; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }

            udp.Clear();
            handler.TerminalListener.OnFrame(TerminatorFrame());

            var eot = Assert.Single(SentOfLength(32));
            Assert.Equal(0x40, eot[16] & 0x40);

            // The stream's count continues into the terminating frame.
            Assert.Equal(0x03, eot[16] & 0x1F);
        }

        [Fact]
        public void FramesArrivingWithoutAHeaderAreIgnored()
        {
            // The radio should not be transmitting, so nothing goes on the air.
            handler.TerminalListener.OnFrame(VoiceFrame(0x00, 0x00));
            handler.TerminalListener.OnFrame(VoiceFrame(0x01, 0x01));

            Assert.Empty(udp.Sent);
        }

        [Fact]
        public void ASecondTransmissionGetsAFreshSessionIdAndRestartsTheCount()
        {
            handler.TerminalListener.OnHeader(RadioHeader());
            for (byte i = 0; i < 4; i++)
            {
                handler.TerminalListener.OnFrame(VoiceFrame(i, i));
            }
            var firstSessionId = SentOfLength(58)[0][14..16];
            handler.TerminalListener.OnFrame(TerminatorFrame());

            udp.Clear();
            handler.TerminalListener.OnHeader(RadioHeader());
            handler.TerminalListener.OnFrame(VoiceFrame(0x00, 0x00));

            Assert.Equal(0x00, Assert.Single(SentOfLength(29))[16]);
            Assert.NotEqual(firstSessionId, SentOfLength(58)[0][14..16]);
        }
    }
}
