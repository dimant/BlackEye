namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialFrame : IcomSerialPacket
    {
        public byte PacketIdHigh { get { return buffer[1]; } }

        public byte PacketIdLow { get { return buffer[2]; } }

        public short PacketId
        {
            get
            {
                ushort high = (ushort)(PacketIdHigh << 8);
                return (short)(high & PacketIdLow);
            }
        }

        public byte[] Ambe { get { return this.buffer[1..9]; } }

        public byte[] Data { get { return this.buffer[10..12]; } }

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
