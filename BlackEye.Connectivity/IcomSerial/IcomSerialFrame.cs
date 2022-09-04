namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialFrame : IcomSerialPacket
    {
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
