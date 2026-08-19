using System.Text;

namespace BlackEye.Connectivity.IcomTerminal
{
    public class IcomTerminalHeader : IcomTerminalPacket, IDStarHeader
    {
        public string Rpt1
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[4..12]);
            }
        }

        public string Rpt2
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[12..20]);
            }
        }

        public string UrCall
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[20..28]);
            }
        }

        public string MyCall
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[28..36]);
            }
        }

        public string Suffix
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[36..40]);
            }
        }

        /// <summary>
        /// CRC over the 39 byte RF header, at stripped bytes 40 and 41, low byte
        /// first. IcomTerminalMode.md notes it can be piped through to the network
        /// without recalculating - but only if the callsigns are piped through too.
        /// </summary>
        public ushort Crc
        {
            get
            {
                return (ushort)(buffer[40] | (buffer[41] << 8));
            }
        }

        public byte RxStatus
        {
            get
            {
                return buffer[42];
            }
        }

        public bool IsCrcValid()
        {
            return DStarCrc.Compute(buffer, 1, 39) == Crc;
        }

        public IcomTerminalHeader(byte[] buffer) : base(buffer)
        {
        }

        public override bool IsValid()
        {
            if (!base.IsValid())
            {
                return false;
            }

            if (!((PacketType)buffer[0] == PacketType.HeaderFromSerial))
            {
                return false;
            }

            return true;
        }
    }
}
