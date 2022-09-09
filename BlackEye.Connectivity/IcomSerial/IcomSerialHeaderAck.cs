namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialHeaderAck : IcomSerialPacket
    {
        public bool Ack { get { return buffer[1] == 0x00; } }

        public byte PacketId { get { return 0x00; } }

        public IcomSerialHeaderAck(byte[] buffer) : base(buffer)
        {
        }

        public override bool IsValid()
        {
            if (!base.IsValid())
            {
                return false;
            }

            if (buffer[1] != 0x00)
            {
                return false;
            }

            return true;
        }
    }
}
