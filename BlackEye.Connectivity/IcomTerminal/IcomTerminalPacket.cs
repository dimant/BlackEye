namespace BlackEye.Connectivity.IcomTerminal
{
    public class IcomTerminalPacket
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

        /// <summary>
        /// Number of bytes the reader handed over. The wire length byte is consumed
        /// during framing and not retained, so a packet with wire length L occupies
        /// L bytes here and code index = wire index - 1 throughout.
        /// </summary>
        public int PayloadLength { get { return buffer.Length; } }

        public PacketType Type { get { return (PacketType)buffer[0]; } }

        public IcomTerminalPacket(byte[] buffer)
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
