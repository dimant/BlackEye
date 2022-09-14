namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialFrame : IcomSerialPacket, IDStarFrame
    {
        public int SequenceId { get { return buffer[1]; } }

        public int Number { get { return buffer[2] & 0x1F; } }

        public int FrameType { get { return buffer[2] & 0xC0; } }

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
