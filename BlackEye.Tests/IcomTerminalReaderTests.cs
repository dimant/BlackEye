namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomTerminal;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks.Dataflow;
    using Xunit;

    /// <summary>
    /// Serial is a continuous byte stream, so the reader has to find packet
    /// boundaries itself: it resyncs on the 0xFF terminator, then reads the length
    /// byte and that many bytes.
    /// </summary>
    public class IcomTerminalReaderTests
    {
        /// <summary>
        /// Feeds a byte stream and drains it packet by packet. The reader blocks on
        /// an empty block, so the stream must contain whole packets only.
        /// </summary>
        private static RecordingTerminalListener ReadAll(params byte[][] wirePackets)
        {
            var listener = new RecordingTerminalListener();
            var reader = new IcomTerminalReader(listener);
            var block = new BufferBlock<byte>();

            // The reader starts by hunting for a 0xFF followed by a non-0xFF, so a
            // stream has to open with a terminator for the first packet to be seen.
            block.Post((byte)0xFF);

            foreach (var packet in wirePackets)
            {
                foreach (var b in packet)
                {
                    block.Post(b);
                }
            }

            for (int i = 0; i < wirePackets.Length; i++)
            {
                reader.Receive(block);
            }

            return listener;
        }

        [Fact]
        public void NullListenerIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new IcomTerminalReader(null!));
        }

        [Fact]
        public void ReadsAPong()
        {
            var listener = ReadAll(CaptureBytes.IcomPongWire);

            var pong = Assert.Single(listener.Pongs);
            Assert.Equal(IcomTerminalPong.PongPacketType.Pong, pong.PongType);
        }

        [Fact]
        public void ReadsTheGoAheadPong()
        {
            var listener = ReadAll(CaptureBytes.IcomPongGoAheadWire);

            var pong = Assert.Single(listener.Pongs);
            Assert.Equal(IcomTerminalPong.PongPacketType.Ack, pong.PongType);
        }

        [Fact]
        public void ReadsAHeader()
        {
            var listener = ReadAll(CaptureBytes.IcomHeaderFromRadioWire);

            var header = Assert.Single(listener.Headers);
            Assert.Equal("AI6VW   ", header.MyCall);
        }

        [Fact]
        public void ReadsAHeaderAck()
        {
            var listener = ReadAll(CaptureBytes.IcomHeaderAckWire);

            var ack = Assert.Single(listener.HeaderAcks);
            Assert.True(ack.Ack);
        }

        [Fact]
        public void ReadsAFrameAck()
        {
            var listener = ReadAll(CaptureBytes.IcomFrameAckWire);

            var ack = Assert.Single(listener.FrameAcks);
            Assert.Equal(0x07, ack.PacketId);
            Assert.True(ack.Ack);
        }

        [Fact]
        public void ReadsAWholeTransmissionBackToBack()
        {
            var listener = ReadAll(
                CaptureBytes.IcomHeaderFromRadioWire,
                CaptureBytes.IcomFrameFromRadioWire,
                CaptureBytes.IcomSecondFrameFromRadioWire,
                CaptureBytes.IcomEotFrameFromRadioWire);

            Assert.Single(listener.Headers);
            Assert.Equal(3, listener.Frames.Count);
            Assert.Equal(new byte[] { 0x00, 0x01, 0x56 }, listener.Frames.Select(f => f.SequenceId).ToArray());
            Assert.False(listener.Frames[0].IsLast());
            Assert.False(listener.Frames[1].IsLast());
            Assert.True(listener.Frames[2].IsLast());
        }

        [Fact]
        public void ResyncsAfterAResetBurst()
        {
            // The app resyncs the radio by spamming 0xFF; the reader must skip the
            // run of terminators and pick up the next real packet.
            var listener = new RecordingTerminalListener();
            var reader = new IcomTerminalReader(listener);
            var block = new BufferBlock<byte>();

            var stream = new List<byte> { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
            stream.AddRange(CaptureBytes.IcomPongWire);
            foreach (var b in stream)
            {
                block.Post(b);
            }

            reader.Receive(block);

            Assert.Single(listener.Pongs);
        }

        [Fact]
        public void PacketsWithoutTheTerminatorAreDropped()
        {
            // A pong whose final byte is not 0xFF fails IsValid and must not be
            // dispatched, but the reader has to stay in sync for the next packet.
            var corrupt = new byte[] { 0x03, 0x03, 0x00, 0x00 };

            var listener = new RecordingTerminalListener();
            var reader = new IcomTerminalReader(listener);
            var block = new BufferBlock<byte>();

            block.Post((byte)0xFF);
            foreach (var b in corrupt)
            {
                block.Post(b);
            }
            block.Post((byte)0xFF);
            foreach (var b in CaptureBytes.IcomPongWire)
            {
                block.Post(b);
            }

            reader.Receive(block);
            reader.Receive(block);

            Assert.Single(listener.Pongs);
        }

        [Fact]
        public void UnknownPacketTypesAreIgnoredWithoutBreakingTheStream()
        {
            var unknown = new byte[] { 0x03, 0x7E, 0x00, 0xFF };

            var listener = ReadAll(unknown, CaptureBytes.IcomPongWire);

            Assert.Single(listener.Pongs);
            Assert.Equal(1, listener.TotalCallbacks);
        }

        [Fact]
        public void BytesArrivingInArbitraryChunksAreReassembled()
        {
            // The serial port hands over whatever happened to be buffered, which
            // splits packets at arbitrary points.
            var listener = new RecordingTerminalListener();
            var reader = new IcomTerminalReader(listener);
            var cancellation = new CancellationTokenSource();

            reader.Start(cancellation.Token);

            try
            {
                reader.OnReceived(new byte[] { 0xFF, 0x2c, 0x10, 0x00 });
                reader.OnReceived(CaptureBytes.IcomHeaderFromRadioWire[3..20]);
                reader.OnReceived(CaptureBytes.IcomHeaderFromRadioWire[20..]);
                reader.OnReceived(CaptureBytes.IcomFrameFromRadioWire);

                Assert.True(Wait.Until(() => listener.Headers.Count == 1 && listener.Frames.Count == 1),
                    "expected the split header and the following frame");
                Assert.Equal("DIRECT  ", listener.Headers[0].Rpt1);
                Assert.Equal(0x00, listener.Frames[0].Number);
            }
            finally
            {
                cancellation.Cancel();
                // Unblock the reader loop so it can observe the cancellation.
                reader.OnReceived(new byte[] { 0xFF, 0x02, 0x02, 0xFF });
            }
        }
    }
}
