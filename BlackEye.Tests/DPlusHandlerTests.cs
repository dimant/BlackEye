namespace BlackEye.Tests
{
    using BlackEye;
    using BlackEye.Connectivity;
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

        private static readonly GatewayConfig Config =
            new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');

        public DPlusHandlerTests()
        {
            handler = NewHandler();

            // Every test below starts from a logged in, idle bridge.
            handler.Connect();
            handler.DPlusListener.OnConnectAck();
            handler.DPlusListener.OnLoginAck(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginAck));

            udp.Clear();
            serial.Clear();
        }

        private DPlusHandler NewHandler() =>
            new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), serial, udp, Config);

        /// <summary>
        /// Everything the bridge sent to the network except keepalives, which the
        /// ping timer emits on its own schedule once the login succeeds.
        /// </summary>
        private byte[][] NetworkTraffic() =>
            udp.Sent.Where(b => b.Length != 3).ToArray();

        private static DPlusHeaderPacket NetworkHeader() =>
            new DPlusHeaderPacket(CaptureBytes.DPlusHeader);

        private static DPlusFramePacket NetworkFrame(byte packetId) =>
            new DPlusFramePacket(CaptureBytes.DPlusVoiceFrame(packetId));

        private static DPlusFramePacket NetworkEot() =>
            new DPlusFramePacket(CaptureBytes.DPlusFrameEot);

        private static IcomTerminalFrameAck FrameAck(byte packetId) =>
            new IcomTerminalFrameAck(CaptureBytes.Stripped(new byte[] { 0x04, 0x23, packetId, 0x00, 0xff }));

        /// <summary>Frames the bridge wrote to the radio, i.e. type 0x22.</summary>
        private byte[][] RadioFrames() =>
            serial.Sent.Where(b => b.Length == 17 && b[1] == 0x22).ToArray();

        private void StartReceiveStream()
        {
            handler.DPlusListener.OnHeader(NetworkHeader());
            serial.Clear();
        }

        private void AckFrames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                handler.TerminalListener.OnFrameAck(FrameAck((byte)i));
            }
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

            Assert.Empty(NetworkTraffic());
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

        // ---------------------------------------------------------------
        // Network to radio: ack-pulled, with filler frames keeping the
        // radio's timing alive when no audio has arrived.
        // ---------------------------------------------------------------

        [Fact]
        public void ANetworkHeaderIsForwardedToTheRadio()
        {
            handler.DPlusListener.OnHeader(NetworkHeader());

            var header = Assert.Single(serial.Sent);
            Assert.Equal(42, header.Length);
            Assert.Equal(0x20, header[1]);
            Assert.Equal("AI6VW  D", System.Text.Encoding.UTF8.GetString(header[5..13]));
            Assert.Equal("REF030 C", System.Text.Encoding.UTF8.GetString(header[13..21]));
        }

        [Fact]
        public void NothingIsSentToTheRadioUntilItAcks()
        {
            StartReceiveStream();

            handler.DPlusListener.OnFrame(NetworkFrame(0x01));
            handler.DPlusListener.OnFrame(NetworkFrame(0x02));

            Assert.Empty(RadioFrames());
        }

        [Fact]
        public void TheFirstRadioBoundFrameIsNumberedZero()
        {
            StartReceiveStream();
            handler.DPlusListener.OnFrame(NetworkFrame(0x11));

            AckFrames(1);

            var frame = Assert.Single(RadioFrames());
            Assert.Equal(0x00, frame[2]);
            Assert.Equal(0x00, frame[3]);
        }

        [Fact]
        public void QueuedAudioIsSentOneFramePerAck()
        {
            StartReceiveStream();
            handler.DPlusListener.OnFrame(NetworkFrame(0x01));
            handler.DPlusListener.OnFrame(NetworkFrame(0x02));

            AckFrames(2);

            var frames = RadioFrames();
            Assert.Equal(2, frames.Length);
            Assert.Equal(new byte[] { 0x00, 0x01 }, frames.Select(f => f[2]).ToArray());
            Assert.Equal(new byte[] { 0x00, 0x01 }, frames.Select(f => f[3]).ToArray());
            Assert.All(frames, f => Assert.Equal(NetworkFrame(0x01).AmbeAndData, f[4..16]));
        }

        [Fact]
        public void FillerFramesAdvanceTheCountersToo()
        {
            // Nothing queued, so every ack produces a filler. If fillers did not
            // advance the ids the radio would see the same frame number repeatedly.
            StartReceiveStream();

            AckFrames(3);

            var frames = RadioFrames();
            Assert.Equal(3, frames.Length);
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, frames.Select(f => f[2]).ToArray());
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, frames.Select(f => f[3]).ToArray());
        }

        [Fact]
        public void TheIdsRunContinuouslyAcrossFillerAndAudio()
        {
            StartReceiveStream();

            AckFrames(2);
            handler.DPlusListener.OnFrame(NetworkFrame(0x05));
            handler.TerminalListener.OnFrameAck(FrameAck(0x02));

            var frames = RadioFrames();
            Assert.Equal(3, frames.Length);
            Assert.Equal(new byte[] { 0x00, 0x01, 0x02 }, frames.Select(f => f[3]).ToArray());

            // The third frame is the queued audio, not another filler.
            Assert.Equal(NetworkFrame(0x05).AmbeAndData, frames[2][4..16]);
        }

        [Fact]
        public void TheFrameNumberWrapsAtTwentyWhileTheSequenceIdKeepsCounting()
        {
            StartReceiveStream();

            AckFrames(22);

            var frames = RadioFrames();
            Assert.Equal(22, frames.Length);
            Assert.Equal(0x14, frames[20][3]);
            Assert.Equal(0x00, frames[21][3]);
            Assert.Equal(0x15, frames[21][2]);
        }

        [Fact]
        public void TheSyncFrameIsSentAtNumberZeroAndEmptyDataOtherwise()
        {
            StartReceiveStream();

            AckFrames(2);

            var frames = RadioFrames();
            Assert.Equal(new byte[] { 0x55, 0x2d, 0x16 }, frames[0][13..16]);
            Assert.Equal(new byte[] { 0x16, 0x29, 0xf5 }, frames[1][13..16]);
        }

        [Fact]
        public void TheNetworkEndOfTransmissionReachesTheRadio()
        {
            StartReceiveStream();
            handler.DPlusListener.OnFrame(NetworkEot());

            AckFrames(1);

            var eot = Assert.Single(RadioFrames());
            Assert.Equal(0x40, eot[3] & 0x40);
            Assert.Equal(new byte[] { 0x55, 0xc8, 0x7a }, eot[4..7]);
        }

        [Fact]
        public void TheStreamEndsOnlyOnceTheEndOfTransmissionHasBeenSent()
        {
            StartReceiveStream();
            handler.DPlusListener.OnFrame(NetworkFrame(0x01));
            handler.DPlusListener.OnFrame(NetworkEot());

            // The EOT is queued behind the audio; the stream is still live.
            AckFrames(1);
            Assert.Single(RadioFrames());

            AckFrames(1);
            Assert.Equal(2, RadioFrames().Length);

            // Now it has gone out, further acks produce nothing.
            AckFrames(2);
            Assert.Equal(2, RadioFrames().Length);
        }

        [Fact]
        public void ASecondStreamStartsFromZeroAgain()
        {
            StartReceiveStream();
            AckFrames(3);
            handler.DPlusListener.OnFrame(NetworkEot());
            AckFrames(1);

            handler.DPlusListener.OnHeader(NetworkHeader());
            serial.Clear();
            AckFrames(1);

            var frame = Assert.Single(RadioFrames());
            Assert.Equal(0x00, frame[2]);
            Assert.Equal(0x00, frame[3]);
        }

        [Fact]
        public void AStreamIsTornDownAfterTooManyMissingFrames()
        {
            StartReceiveStream();

            AckFrames(100);
            Assert.Equal(100, RadioFrames().Length);
            Assert.All(RadioFrames(), f => Assert.Equal(0x00, f[3] & 0x40));

            // The 101st missing frame gives up: the empty voice last frame is
            // followed by the end of transmission frame, as the doc requires.
            AckFrames(1);

            var frames = RadioFrames();
            Assert.Equal(102, frames.Length);
            Assert.Equal(new byte[] { 0x55, 0x55, 0x55 }, frames[100][13..16]);
            Assert.Equal(0x40, frames[101][3] & 0x40);
            Assert.Equal(new byte[] { 0x55, 0xc8, 0x7a }, frames[101][4..7]);
        }

        [Fact]
        public void AudioArrivingInTimeResetsTheMissingFrameCount()
        {
            StartReceiveStream();

            AckFrames(60);
            handler.DPlusListener.OnFrame(NetworkFrame(0x01));
            AckFrames(1);
            AckFrames(60);

            // 121 acks with only one real frame, but never 100 missing in a row,
            // so the stream is still live and nothing was torn down.
            Assert.Equal(121, RadioFrames().Length);
            Assert.All(RadioFrames(), f => Assert.Equal(0x00, f[3] & 0x40));
        }

        [Fact]
        public void FramesFromTheNetworkAreIgnoredWithoutAHeader()
        {
            handler.DPlusListener.OnFrame(NetworkFrame(0x01));
            handler.TerminalListener.OnFrameAck(FrameAck(0x00));

            Assert.Empty(serial.Sent);
        }

        // ---------------------------------------------------------------
        // Connect, login and routing.
        // ---------------------------------------------------------------

        [Fact]
        public void ConnectSendsTheConnectPacket()
        {
            var fresh = new FakeConnection();
            var bridge = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), new FakeConnection(), fresh, Config);

            bridge.Connect();

            Assert.Equal(CaptureBytes.DPlusConnect, Assert.Single(fresh.Sent));
        }

        [Fact]
        public void TheServersEchoIsAnsweredWithALogin()
        {
            var fresh = new FakeConnection();
            var bridge = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), new FakeConnection(), fresh, Config);

            bridge.Connect();
            fresh.Clear();
            bridge.DPlusListener.OnConnectAck();

            Assert.Equal(CaptureBytes.DPlusLogin, Assert.Single(fresh.Sent));
        }

        [Fact]
        public void NothingIsTransmittedBeforeTheLoginIsAccepted()
        {
            var fresh = new FakeConnection();
            var radio = new FakeConnection();
            var bridge = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), radio, fresh, Config);

            bridge.Connect();
            bridge.DPlusListener.OnConnectAck();
            fresh.Clear();

            // The radio keys up before the reflector has let us in.
            bridge.TerminalListener.OnHeader(RadioHeader());
            bridge.TerminalListener.OnFrame(VoiceFrame(0x00, 0x00));

            Assert.Empty(fresh.Sent);
        }

        [Fact]
        public void ARejectedLoginLeavesTheBridgeDisconnected()
        {
            var fresh = new FakeConnection();
            var bridge = new DPlusHandler(new DPlusNetworkWriter(), new IcomTerminalWriter(), new FakeConnection(), fresh, Config);

            bridge.Connect();
            bridge.DPlusListener.OnConnectAck();
            bridge.DPlusListener.OnLoginAck(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginNak));
            fresh.Clear();

            bridge.TerminalListener.OnHeader(RadioHeader());

            Assert.Empty(fresh.Sent);
        }

        [Fact]
        public void TheOutboundHeaderCarriesTheReflectorNotTheRadiosDirectCallsigns()
        {
            // In terminal mode the radio sends DIRECT in both repeater fields, and
            // the reflector verifies mycall and rpt2 before letting a transmission
            // on the air.
            Assert.Equal("DIRECT  ", RadioHeader().Rpt1);
            Assert.Equal("DIRECT  ", RadioHeader().Rpt2);

            handler.TerminalListener.OnHeader(RadioHeader());

            var header = SentOfLength(58)[0];
            Assert.Equal("REF030 C", System.Text.Encoding.UTF8.GetString(header[20..28]));
            Assert.Equal("AI6VW  D", System.Text.Encoding.UTF8.GetString(header[28..36]));
            Assert.Equal("AI6VW   ", System.Text.Encoding.UTF8.GetString(header[44..52]));
            Assert.Equal("ID52", System.Text.Encoding.UTF8.GetString(header[52..56]));
        }

        [Fact]
        public void TheOperatorsUrcallIsForwardedUnchanged()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            var header = SentOfLength(58)[0];
            Assert.Equal("CQCQCQ  ", System.Text.Encoding.UTF8.GetString(header[36..44]));
        }

        [Fact]
        public void TheOutboundHeaderCrcDescribesTheSubstitutedCallsigns()
        {
            handler.TerminalListener.OnHeader(RadioHeader());

            var header = SentOfLength(58)[0];
            var crc = DStarCrc.Compute(header, 17, 39);

            Assert.Equal((byte)(crc & 0xFF), header[56]);
            Assert.Equal((byte)(crc >> 8), header[57]);
        }

        [Fact]
        public void AReflectorChangeIsReflectedOnTheAir()
        {
            var fresh = new FakeConnection();
            var bridge = new DPlusHandler(
                new DPlusNetworkWriter(), new IcomTerminalWriter(), new FakeConnection(), fresh,
                new GatewayConfig("W1AW", "HT", "XLX999", 'B', 'A'));

            bridge.Connect();
            bridge.DPlusListener.OnConnectAck();
            bridge.DPlusListener.OnLoginAck(new DPlusLoginAckPacket(CaptureBytes.DPlusLoginAck));
            fresh.Clear();

            bridge.TerminalListener.OnHeader(RadioHeader());

            var header = fresh.Sent.First(b => b.Length == 58);
            Assert.Equal("XLX999 B", System.Text.Encoding.UTF8.GetString(header[20..28]));
            Assert.Equal("W1AW   A", System.Text.Encoding.UTF8.GetString(header[28..36]));
            Assert.Equal("W1AW    ", System.Text.Encoding.UTF8.GetString(header[44..52]));
            Assert.Equal("HT  ", System.Text.Encoding.UTF8.GetString(header[52..56]));
        }
    }
}
