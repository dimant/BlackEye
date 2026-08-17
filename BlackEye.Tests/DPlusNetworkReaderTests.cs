namespace BlackEye.Tests
{
    using BlackEye.Connectivity.DPlus;
    using System;
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    /// UDP is already message framed, so the reader parses one whole datagram per
    /// call and validates that byte 0 matches the datagram length.
    /// </summary>
    public class DPlusNetworkReaderTests
    {
        private class RecordingDPlusListener : IDPlusListener
        {
            public int ConnectAcks { get; private set; }

            public int EotAcks { get; private set; }

            public int Pongs { get; private set; }

            public List<DPlusLoginAckPacket> LoginAcks { get; } = new List<DPlusLoginAckPacket>();

            public List<DPlusHeaderPacket> Headers { get; } = new List<DPlusHeaderPacket>();

            public List<DPlusFramePacket> Frames { get; } = new List<DPlusFramePacket>();

            public int TotalCallbacks =>
                ConnectAcks + EotAcks + Pongs + LoginAcks.Count + Headers.Count + Frames.Count;

            public void OnConnectAck() => ConnectAcks++;

            public void OnLoginAck(DPlusLoginAckPacket packet) => LoginAcks.Add(packet);

            public void OnHeader(DPlusHeaderPacket packet) => Headers.Add(packet);

            public void OnFrame(DPlusFramePacket packet) => Frames.Add(packet);

            public void OnEotAck() => EotAcks++;

            public void OnPong() => Pongs++;
        }

        private readonly RecordingDPlusListener listener = new RecordingDPlusListener();

        private readonly DPlusNetworkReader reader;

        public DPlusNetworkReaderTests()
        {
            reader = new DPlusNetworkReader(listener);
        }

        [Fact]
        public void NullListenerIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DPlusNetworkReader(null!));
        }

        [Fact]
        public void NullDatagramIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => reader.Receive(null!));
        }

        [Fact]
        public void DatagramShorterThanAHeaderIsRejected()
        {
            Assert.Throws<ArgumentException>(() => reader.Receive(new byte[] { 0x01 }));
        }

        [Fact]
        public void DatagramWhoseLengthByteDisagreesWithItsSizeIsRejected()
        {
            // The DPlus length byte counts itself, so this must be 5, not 4.
            Assert.Throws<ArgumentException>(() => reader.Receive(new byte[] { 0x04, 0x00, 0x18, 0x00, 0x01 }));
        }

        [Fact]
        public void ConnectEchoIsReportedAsAnAck()
        {
            reader.Receive(CaptureBytes.DPlusConnect);

            Assert.Equal(1, listener.ConnectAcks);
        }

        [Fact]
        public void DisconnectIsNotMistakenForAConnectAck()
        {
            reader.Receive(CaptureBytes.DPlusDisconnect);

            Assert.Equal(0, listener.TotalCallbacks);
        }

        [Fact]
        public void PongIsReported()
        {
            reader.Receive(CaptureBytes.DPlusPing);

            Assert.Equal(1, listener.Pongs);
        }

        [Fact]
        public void SuccessfulLoginIsReported()
        {
            reader.Receive(CaptureBytes.DPlusLoginAck);

            var ack = Assert.Single(listener.LoginAcks);
            Assert.True(ack.Ack);
        }

        [Fact]
        public void RejectedLoginIsReportedAsNotAcknowledged()
        {
            reader.Receive(CaptureBytes.DPlusLoginNak);

            var ack = Assert.Single(listener.LoginAcks);
            Assert.False(ack.Ack);
        }

        [Fact]
        public void EndOfTransmissionAckIsReported()
        {
            reader.Receive(CaptureBytes.DPlusEotAck);

            Assert.Equal(1, listener.EotAcks);
        }

        [Fact]
        public void HeaderIsDispatchedOnByteSixBeingSixteen()
        {
            reader.Receive(CaptureBytes.DPlusHeader);

            var header = Assert.Single(listener.Headers);
            Assert.Equal("REF030 C", header.Rpt2);
        }

        [Fact]
        public void FrameIsDispatchedOnByteSixBeingThirtyTwo()
        {
            reader.Receive(CaptureBytes.DPlusFrame);

            Assert.Single(listener.Frames);
        }

        [Fact]
        public void EndOfTransmissionFrameArrivesAsAFrame()
        {
            reader.Receive(CaptureBytes.DPlusFrameEot);

            var frame = Assert.Single(listener.Frames);
            Assert.Equal(32, frame.Length);
        }

        [Fact]
        public void UnknownPacketTypesAreIgnored()
        {
            reader.Receive(new byte[] { 0x03, 0x7e, 0x00 });

            Assert.Equal(0, listener.TotalCallbacks);
        }

        [Fact]
        public void ADsvtPacketWithAnUnknownByteSixIsIgnored()
        {
            var unknown = (byte[])CaptureBytes.DPlusFrame.Clone();
            unknown[6] = 0x30;

            reader.Receive(unknown);

            Assert.Equal(0, listener.TotalCallbacks);
        }

        [Fact]
        public void AWholeStreamIsDispatchedInOrder()
        {
            reader.Receive(CaptureBytes.DPlusHeader);
            reader.Receive(CaptureBytes.DPlusVoiceFrame(0x0f));
            reader.Receive(CaptureBytes.DPlusVoiceFrame(0x10));
            reader.Receive(CaptureBytes.DPlusFrameEot);

            Assert.Single(listener.Headers);
            Assert.Equal(3, listener.Frames.Count);
        }
    }
}
