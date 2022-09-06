using System.Text;

namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialHeader : IcomSerialPacket
    {
        public string Rpt1
        {   get
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

        public IcomSerialHeader(byte[] buffer) : base(buffer)
        {
        }

        public override bool IsValid()
        {
            if(!base.IsValid())
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
