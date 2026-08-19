namespace BlackEye.Connectivity.IcomTerminal
{
    public class IcomTerminalFrame : IcomTerminalPacket, IDStarFrame
    {
        public byte SequenceId { get { return buffer[1]; } }

        public byte Number { get { return (byte) (buffer[2] & 0x1F); } }

        public byte FrameType { get { return (byte) (buffer[2] & 0xC0); } }

        public byte[] AmbeAndData { get { return this.buffer[3..15]; } }

        public byte[] Ambe { get { return this.buffer[3..12]; } }

        public byte[] Data { get { return this.buffer[12..15]; } }

        public IcomTerminalFrame(byte[] buffer) : base(buffer)
        {
        }

        /// <summary>
        /// The radio marks its terminator frame with bit 0x40 in the frame type
        /// bits: dumps/rs-ms3w-rs232-dump.txt shows 10 12 56 42 55 c8 7a 00...
        /// directly after the last voice frame. Testing for zero bytes in the
        /// payload instead also matched ordinary AMBE.
        /// </summary>
        public bool IsLast()
        {
            return (FrameType & 0x40) == 0x40;
        }

        public override bool IsValid()
        {
            if (!base.IsValid())
            {
                return false;
            }

            if (!((PacketType)buffer[0] == PacketType.FrameFromSerial))
            {
                return false;
            }

            return true;
        }
    }
}
