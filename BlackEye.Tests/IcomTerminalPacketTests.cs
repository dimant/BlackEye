namespace BlackEye.Tests
{
    using BlackEye.Connectivity.IcomTerminal;
    using System;
    using Xunit;

    /// <summary>
    /// The reader strips the wire length byte before constructing a packet, so
    /// every offset here is one lower than the offset in IcomTerminalMode.md.
    /// </summary>
    public class IcomTerminalPacketTests
    {
        [Fact]
        public void NullBufferIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new IcomTerminalHeader(null!));
            Assert.Throws<ArgumentNullException>(() => new IcomTerminalFrame(null!));
        }

        [Fact]
        public void HeaderCallsignOffsetsMatchTheCapture()
        {
            var header = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal("DIRECT  ", header.Rpt1);
            Assert.Equal("DIRECT  ", header.Rpt2);
            Assert.Equal("CQCQCQ  ", header.UrCall);
            Assert.Equal("AI6VW   ", header.MyCall);
            Assert.Equal("ID52", header.Suffix);
        }

        [Fact]
        public void HeaderIsValidWithItsTerminator()
        {
            var header = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.True(header.IsValid());
        }

        [Fact]
        public void HeaderWithoutATerminatorIsInvalid()
        {
            var wire = (byte[])CaptureBytes.IcomHeaderFromRadioWire.Clone();
            wire[^1] = 0x00;

            var header = new IcomTerminalHeader(CaptureBytes.Stripped(wire));

            Assert.False(header.IsValid());
        }

        [Fact]
        public void HeaderOfTheWrongTypeIsInvalid()
        {
            var wire = (byte[])CaptureBytes.IcomHeaderFromRadioWire.Clone();
            wire[1] = 0x20;   // header to the radio, not from it

            var header = new IcomTerminalHeader(CaptureBytes.Stripped(wire));

            Assert.False(header.IsValid());
        }

        [Fact]
        public void FramePayloadIsNineAmbePlusThreeSlowData()
        {
            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomFrameFromRadioWire));

            Assert.Equal(9, frame.Ambe.Length);
            Assert.Equal(3, frame.Data.Length);
            Assert.Equal(12, frame.AmbeAndData.Length);
            Assert.Equal(new byte[] { 0xb2, 0x4d, 0x22, 0x48, 0xc0, 0x16, 0x28, 0x26, 0xc8 }, frame.Ambe);
            Assert.Equal(new byte[] { 0x55, 0x2d, 0x16 }, frame.Data);
            Assert.Equal(frame.Ambe, frame.AmbeAndData[0..9]);
            Assert.Equal(frame.Data, frame.AmbeAndData[9..12]);
        }

        [Fact]
        public void FrameSequenceIdAndNumberAreSeparateFields()
        {
            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomSecondFrameFromRadioWire));

            Assert.Equal(0x01, frame.SequenceId);
            Assert.Equal(0x01, frame.Number);
            Assert.Equal(0x00, frame.FrameType);
        }

        [Fact]
        public void TheNumberFieldMasksOffTheFlagBits()
        {
            // Byte 3 carries flags in the top bits and the 0..20 number in 0x1F.
            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomEotFrameFromRadioWire));

            Assert.Equal(0x02, frame.Number);
            Assert.Equal(0x40, frame.FrameType);
        }

        [Fact]
        public void TheRadiosTerminatorFrameIsRecognisedAsLast()
        {
            var terminator = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomEotFrameFromRadioWire));

            Assert.True(terminator.IsLast());
        }

        [Fact]
        public void OrdinaryVoiceFramesAreNotLast()
        {
            var first = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomFrameFromRadioWire));
            var second = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomSecondFrameFromRadioWire));

            Assert.False(first.IsLast());
            Assert.False(second.IsLast());
        }

        [Fact]
        public void FrameOfTheWrongTypeIsInvalid()
        {
            var wire = (byte[])CaptureBytes.IcomFrameFromRadioWire.Clone();
            wire[1] = 0x22;   // frame to the radio, not from it

            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(wire));

            Assert.False(frame.IsValid());
        }

        [Fact]
        public void PongFlagDistinguishesReadyToSendFromReadyToReceive()
        {
            var pong = new IcomTerminalPong(CaptureBytes.Stripped(CaptureBytes.IcomPongWire));
            var goAhead = new IcomTerminalPong(CaptureBytes.Stripped(CaptureBytes.IcomPongGoAheadWire));

            Assert.Equal(IcomTerminalPong.PongPacketType.Pong, pong.PongType);
            Assert.Equal(IcomTerminalPong.PongPacketType.Ack, goAhead.PongType);
            Assert.True(pong.IsValid());
            Assert.True(goAhead.IsValid());
        }

        [Fact]
        public void PongWithAnUndefinedFlagIsInvalid()
        {
            var pong = new IcomTerminalPong(CaptureBytes.Stripped(new byte[] { 0x03, 0x03, 0x7f, 0xff }));

            Assert.False(pong.IsValid());
        }

        [Fact]
        public void PongOfTheWrongTypeIsInvalid()
        {
            var pong = new IcomTerminalPong(CaptureBytes.Stripped(new byte[] { 0x03, 0x02, 0x00, 0xff }));

            Assert.False(pong.IsValid());
        }

        [Fact]
        public void PongWithoutATerminatorIsInvalid()
        {
            var pong = new IcomTerminalPong(CaptureBytes.Stripped(new byte[] { 0x03, 0x03, 0x00, 0x00 }));

            Assert.False(pong.IsValid());
        }

        [Fact]
        public void FrameWithoutATerminatorIsInvalid()
        {
            var wire = (byte[])CaptureBytes.IcomFrameFromRadioWire.Clone();
            wire[^1] = 0x00;

            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(wire));

            Assert.False(frame.IsValid());
        }

        [Fact]
        public void HeaderAckHasNoPacketIdOfItsOwn()
        {
            // Header acks carry only a flag, so the packet id is reported as zero
            // to keep the shape uniform with frame acks.
            var ack = new IcomTerminalHeaderAck(CaptureBytes.Stripped(CaptureBytes.IcomHeaderAckWire));

            Assert.Equal(0x00, ack.PacketId);
        }

        [Fact]
        public void FrameAckCarriesThePacketIdAndTheReadyStatus()
        {
            var ready = new IcomTerminalFrameAck(CaptureBytes.Stripped(CaptureBytes.IcomFrameAckWire));
            var notReady = new IcomTerminalFrameAck(CaptureBytes.Stripped(new byte[] { 0x04, 0x23, 0x07, 0x01, 0xff }));

            Assert.Equal(0x07, ready.PacketId);
            Assert.True(ready.Ack);

            // Status 0x01 means the radio's input queue is full and the computer
            // must wait for a second ack before sending more.
            Assert.Equal(0x07, notReady.PacketId);
            Assert.False(notReady.Ack);
        }

        [Fact]
        public void TypeReadsTheStrippedBuffersFirstByte()
        {
            var header = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));
            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(CaptureBytes.IcomFrameFromRadioWire));
            var pong = new IcomTerminalPong(CaptureBytes.Stripped(CaptureBytes.IcomPongWire));
            var frameAck = new IcomTerminalFrameAck(CaptureBytes.Stripped(CaptureBytes.IcomFrameAckWire));

            Assert.Equal(IcomTerminalPacket.PacketType.HeaderFromSerial, header.Type);
            Assert.Equal(IcomTerminalPacket.PacketType.FrameFromSerial, frame.Type);
            Assert.Equal(IcomTerminalPacket.PacketType.Pong, pong.Type);
            Assert.Equal(IcomTerminalPacket.PacketType.FrameToSerialAck, frameAck.Type);
        }

        [Fact]
        public void PayloadLengthIsWhatTheReaderHandedOver()
        {
            // The wire length byte is consumed by the reader and not retained, so
            // an Icom packet with wire length L occupies L bytes here.
            var header = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal(CaptureBytes.IcomHeaderFromRadioWire[0], header.PayloadLength);
            Assert.Equal(0x2c, header.PayloadLength);
        }

        [Fact]
        public void AVoiceFrameWithZeroBytesInTheAmbeIsNotTreatedAsLast()
        {
            // Three zero bytes can occur inside ordinary AMBE. Only the 0x40 bit in
            // byte 3 marks the radio's terminator frame.
            var wire = new byte[]
            {
                0x10, 0x12, 0x33, 0x0a,
                0x4a, 0x7c, 0x1b, 0xef, 0x20, 0xe6, 0xcb, 0x00, 0x00,
                0x00, 0x2d, 0x16,
                0xff
            };

            var frame = new IcomTerminalFrame(CaptureBytes.Stripped(wire));

            Assert.Equal(0x00, frame.FrameType);
            Assert.False(frame.IsLast());
        }

        [Fact]
        public void AnyFrameCarryingTheLastFrameBitIsLast()
        {
            for (byte number = 0; number <= 20; number++)
            {
                var wire = (byte[])CaptureBytes.IcomFrameFromRadioWire.Clone();
                wire[3] = (byte)(number | 0x40);

                Assert.True(new IcomTerminalFrame(CaptureBytes.Stripped(wire)).IsLast());
            }
        }

        [Fact]
        public void HeaderNakSurvivesValidationSoItCanBeReported()
        {
            var nak = new IcomTerminalHeaderAck(CaptureBytes.Stripped(new byte[] { 0x03, 0x21, 0x01, 0xff }));

            Assert.True(nak.IsValid());
            Assert.False(nak.Ack);
        }

        [Fact]
        public void HeaderAckOfTheWrongTypeIsInvalid()
        {
            var ack = new IcomTerminalHeaderAck(CaptureBytes.Stripped(new byte[] { 0x03, 0x23, 0x00, 0xff }));

            Assert.False(ack.IsValid());
        }

        [Fact]
        public void HeaderAckReportsAcceptance()
        {
            var ack = new IcomTerminalHeaderAck(CaptureBytes.Stripped(CaptureBytes.IcomHeaderAckWire));

            Assert.True(ack.Ack);
            Assert.True(ack.IsValid());
        }
    }
}
