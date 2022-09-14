namespace BlackEye.Connectivity.IcomTerminal
{
    public class IcomTerminalFrameAck : IcomTerminalPacket
    {
        public byte PacketId { get { return buffer[1]; } }

        public bool Ack { get { return buffer[2] == 0x00; } }

        public bool IsEotAck()
        {
            return (buffer[1] == 0x80 && buffer[2] == 0x00);
        }

        public IcomTerminalFrameAck(byte[] buffer) : base(buffer)
        {
        }
    }
}
