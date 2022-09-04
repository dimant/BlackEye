using System.Text;

namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialHeader : IcomSerialPacket
    {
        public string To
        {   get
            {
                return Encoding.UTF8.GetString(buffer[5..12]);
            }
        }

        public string From
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[13..20]);
            }
        }

        public string UrCall
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[21..28]);
            }
        }

        public string MyCall
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[29..36]);
            }
        }

        public string Suffix
        {
            get
            {
                return Encoding.UTF8.GetString(buffer[37..40]);
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
