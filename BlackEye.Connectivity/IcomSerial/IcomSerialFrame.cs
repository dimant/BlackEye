namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialFrame : IcomSerialPacket, IDStarFrame
    {
        public int PacketIdHigh { get { return buffer[1]; } }

        public int PacketIdLow { get { return buffer[2]; } }

        public ushort PacketId
        {
            get
            {
                int result = 0x00;
                result |= PacketIdLow;
                result |= PacketIdHigh << 8;
                return (ushort)result;
            }
        }

        public byte[] AmbeAndData { get { return this.buffer[3..15]; } }

        public byte[] Ambe { get { return this.buffer[3..12]; } }

        public byte[] Data { get { return this.buffer[12..15]; } }

        public IcomSerialFrame(byte[] buffer) : base(buffer)
        {
        }

        public bool IsLast()
        {
            if (!(buffer[10] == 0x00 && buffer[11] == 0x00 && buffer[12] == 0x00))
            {
                return false;
            }

            return true;
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
