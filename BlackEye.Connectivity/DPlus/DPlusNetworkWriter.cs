namespace BlackEye.Connectivity.DPlus
{
    using System.Text;

    public class DPlusNetworkWriter
    {
        public byte[] WritePing()
        {
            var buffer = new byte[] { 0x03, 0x60, 0x00 };

            return buffer;
        }

        public byte[] WriteConnect()
        {
            var buffer = new byte[] { 0x05, 0x00, 0x18, 0x00, 0x01 };

            return buffer;
        }

        public byte[] WriteDisconnect()
        {
            var buffer = new byte[] { 0x05, 0x00, 0x18, 0x00, 0x00 };

            return buffer;
        }

        public byte[] WriteLogin(string mycall)
        {
            var buffer = new byte[] {
                0x1C, 0xC0, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x44, 0x56, 0x31, 0x39, 0x39, 0x39, 0x34
            };

            var mycallBytes = Encoding.UTF8.GetBytes(mycall);

            mycallBytes.CopyTo(buffer, 4);

            return buffer;
        }

        public byte[] WriteHeader(string rpt1, string rpt2, string urcall, string mycall, string suffix, short sessionid)
        {
            var rpt1Bytes = Encoding.UTF8.GetBytes(rpt1);
            var rpt2Bytes = Encoding.UTF8.GetBytes(rpt2);
            var urcallBytes = Encoding.UTF8.GetBytes(urcall);
            var mycallBytes = Encoding.UTF8.GetBytes(mycall);
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);
            byte sessionIdHigh = (byte)(sessionid >> 8);
            byte sessionIdLow = (byte)(sessionid & 0xFF);

            var dstarHeader = new byte[36];

            rpt2Bytes.CopyTo(dstarHeader, 0);
            rpt1Bytes.CopyTo(dstarHeader, 8);
            urcallBytes.CopyTo(dstarHeader, 16);
            mycallBytes.CopyTo(dstarHeader, 24);
            suffixBytes.CopyTo(dstarHeader, 32);

            return WriteHeader(dstarHeader, sessionIdHigh, sessionIdLow);
        }

        public byte[] WriteHeader(byte[] dstarHeader, byte sessionIdHigh, byte sessionIdLow)
        {
            var buffer = new byte[58]
            {
                0x3A, 0x80, (byte)'D', (byte)'S', (byte)'V', (byte)'T',
                0x10, 0x00, 0x00, 0x00, 0x20, 0x00, 0x02, 0x01,
                sessionIdHigh, sessionIdLow,
                0x80, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Rpt2
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // Rpt1
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // urcall
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // mycall
                0x00, 0x00, 0x00, 0x00,                         // suffix
                0x00, 0x0B
            };

            dstarHeader.CopyTo(dstarHeader, 20);

            return buffer;
        }

        public byte[] WriteFrame(byte[] ambe, byte[] data, short sessionid, byte packetid)
        {
            byte sessionIdHigh = (byte)(sessionid >> 8);
            byte sessionIdLow = (byte)(sessionid & 0xFF);
            var ambeAndData = new byte[12];

            ambe.CopyTo(ambeAndData, 0);
            data.CopyTo(ambeAndData, 9);

            return WriteFrame(ambeAndData, sessionIdHigh, sessionIdLow, packetid);
        }

        public byte[] WriteFrame(byte[] ambeAndData, short sessionid, byte packetid)
        {
            byte sessionIdHigh = (byte)(sessionid >> 8);
            byte sessionIdLow = (byte)(sessionid & 0xFF);

            return WriteFrame(ambeAndData, sessionIdHigh, sessionIdLow, packetid);
        }

        public byte[] WriteFrame(byte[] ambeAndData, byte sessionIdHigh, byte sessionIdLow, byte packetid)
        {
            var buffer = new byte[29]
            {
                0X1D, 0x80, 0x44, 0x53, 0x56, 0x54, 0x20, 0x00, 0x00, 0x00, 0x20, 0x00, 0x02, 0x01,
                sessionIdHigh, sessionIdLow, packetid,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00
            };

            ambeAndData.CopyTo(buffer, 17);

            return buffer;
        }

        public byte[] WriteFrameEot(short sessionid, byte packetid)
        {
            byte sessionIdHigh = (byte)(sessionid >> 8);
            byte sessionIdLow = (byte)(sessionid & 0xFF);

            var buffer = new byte[32]
            {
                0X1D, 0x80, 0x44, 0x53, 0x56, 0x54, 0x20, 0x00, 0x00, 0x00, 0x20, 0x00, 0x02, 0x01,
                sessionIdHigh, sessionIdLow, packetid,
                0x9E, 0x8D, 0x32, 0x88, 0x26, 0x1A, 0x3F, 0x61, 0xE8,
                0x55, 0x55, 0x55,
                0x55, 0xC8, 0x7A
            };

            return buffer;
        }
    }
}
