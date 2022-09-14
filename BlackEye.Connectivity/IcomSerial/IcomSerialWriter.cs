namespace BlackEye.Connectivity.IcomSerial
{
    using System.Text;

    public class IcomSerialWriter
    {
        public byte[] WritePing()
        {
            byte[] buffer = new byte[] { 0x02, 0x02, 0xff };

            return buffer;
        }

        public byte[] WriteReset()
        {
            var buffer = new byte[3] { 0xff, 0xff, 0xff };

            return buffer;
        }

        public byte[] WriteHeader(string rpt2, string rpt1, string urcall, string mycall, string suffix)
        {
            var rpt1Bytes = Encoding.UTF8.GetBytes(rpt1);
            var rpt2Bytes = Encoding.UTF8.GetBytes(rpt2);
            var urcallBytes = Encoding.UTF8.GetBytes(urcall);
            var mycallBytes = Encoding.UTF8.GetBytes(mycall);
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);

            var dstarHeader = new byte[36];
            rpt1Bytes.CopyTo(dstarHeader, 0);
            rpt2Bytes.CopyTo(dstarHeader, 8);
            urcallBytes.CopyTo(dstarHeader, 16);
            mycallBytes.CopyTo(dstarHeader, 24);
            suffixBytes.CopyTo(dstarHeader, 32);

            return WriteHeader(dstarHeader);
        }

        public byte[] WriteHeader(byte[] dstarHeader)
        {
            var tag = new byte[] { 0x29, 0x20, 0x01, 0x00, 0x00 };
            var buffer = new byte[42];
            tag.CopyTo(buffer, 0);
            dstarHeader.CopyTo(buffer, 5);
            buffer[41] = 0xff;

            return buffer;
        }

        public byte[] WriteFrame(byte sequenceId, byte number, byte[] ambeAndData)
        {
            if (ambeAndData.Length != 12)
            {
                throw new ArgumentException($"{nameof(ambeAndData)} must be 12 bytes. 9 bytes AMBE and 3 bytes data.");
            }

            var tag = new byte[] { 0x10, 0x22, sequenceId, number};
            var buffer = new byte[17];
            tag.CopyTo(buffer, 0);
            ambeAndData.CopyTo(buffer, 4);
            buffer[16] = 0xff;

            return buffer;
        }

        public byte[] WriteFrameEot()
        {
            byte[] buffer = new byte[17] {
                0x10, 0x22, 0x08, 0x48, 0x55, 0xc8, 0x7a, 0x55,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                0xff
            };

            return buffer;
        }
    }
}
