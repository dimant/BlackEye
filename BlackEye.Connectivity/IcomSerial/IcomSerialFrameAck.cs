namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialFrameAck : IcomSerialPacket
    {
        public byte PacketIdHigh { get { return buffer[1]; } }

        public byte PacketIdLow { get { return buffer[2]; } }

        public bool IsEotAck()
        {
            return (buffer[1] == 0x80 && buffer[2] == 0x00);
        }

        public IcomSerialFrameAck(byte[] buffer) : base(buffer)
        {
        }
    }
}
