namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialPacket
    {
        public enum PacketType
        {
            Ping = 0x02,
            Pong = 0x03,
            HeaderFromSerial = 0x10,
            FrameFromSerial = 0x12,
            HeaderToSerial = 0x20,
            HeaderToSerialAck = 0x21,
            FrameToSerial = 0x22,
            FrameToSerialAck = 0x23
        }

        protected byte[] buffer;

        public int Length { get { return buffer[0]; } }

        public PacketType Type { get { return (PacketType)buffer[1]; } }

        public byte[] Buffer { set { buffer = value; } }

        public IcomSerialPacket(byte[] buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public virtual bool IsValid()
        {
            if (!(buffer[buffer.Length - 1] == 0xff))
            {
                return false;
            }

            return true;
        }
    }
}
