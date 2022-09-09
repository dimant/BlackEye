namespace BlackEye.Connectivity.DPlus
{
    public abstract class DPlusPacket
    {
        public enum PacketType
        {
            LoginAck,
            EotAck,
            Pong,
            Header,
            Frame,
            FrameEot
        }

        protected byte[] buffer;

        protected PacketType packetType;

        public int Length { get { return buffer[0]; } }

        public PacketType Type { get { return packetType; } }

        public DPlusPacket(byte[] buffer)
        {
            this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }
    }
}
