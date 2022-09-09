using System.Text;

namespace BlackEye.Connectivity.IcomSerial
{
    public class IcomSerialWriter
    {
        private IConnection serialConnection;

        public IcomSerialWriter(IConnection serialConnection)
        {
            this.serialConnection = serialConnection ?? throw new ArgumentNullException(nameof(serialConnection));
        }

        public void SendPing()
        {
            byte[] cmd = new byte[] { 0x02, 0x02, 0xff };

            serialConnection.Send(cmd);
        }

        public void SendReset()
        {
            for (int i = 0; i < 5; i++)
            {
                serialConnection.Send(new byte[3] { 0xff, 0xff, 0xff });
            }
        }

        public void SendPoll()
        {
            byte[] buffer = new byte[] { 0xff, 0xff, 0xff };

            serialConnection.Send(buffer);
        }

        public void SendHeader(string to, string from, string urcall, string mycall, string suffix)
        {
            var fromBytes = Encoding.UTF8.GetBytes(from);
            var toBytes = Encoding.UTF8.GetBytes(to);
            var urcallBytes = Encoding.UTF8.GetBytes(urcall);
            var mycallBytes = Encoding.UTF8.GetBytes(mycall);
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);

            var dstarHeader = new byte[36];
            fromBytes.CopyTo(dstarHeader, 0);
            toBytes.CopyTo(dstarHeader, 8);
            urcallBytes.CopyTo(dstarHeader, 16);
            mycallBytes.CopyTo(dstarHeader, 24);
            suffixBytes.CopyTo(dstarHeader, 32);

            this.SendHeader(dstarHeader);
        }

        public void SendHeader(byte[] dstarHeader)
        {
            var tag = new byte[] { 0x29, 0x20, 0x01, 0x00, 0x00 };
            var buffer = new byte[42];
            tag.CopyTo(buffer, 0);
            dstarHeader.CopyTo(buffer, 5);
            buffer[41] = 0xff;

            serialConnection.Send(buffer);
        }

        public void SendFrame(ushort packetId, byte[] data)
        {
            if (data.Length != 12)
            {
                throw new ArgumentException($"{nameof(data)} must be 12 bytes. 9 bytes AMBE and 3 bytes data.");
            }

            byte packetIdLow = (byte) (packetId & 0xff);
            byte packetIdHigh = (byte) ((packetId >> 8) & 0xff);
            var tag = new byte[] { 0x10, 0x22, packetIdHigh, packetIdLow};
            var buffer = new byte[17];
            tag.CopyTo(buffer, 0);
            data.CopyTo(buffer, 4);
            buffer[16] = 0xff;

            serialConnection.Send(buffer);
        }

        public void SendFrameEot()
        {
            byte[] buffer = new byte[17] {
                0x10, 0x22, 0x08, 0x48, 0x55, 0xc8, 0x7a, 0x55,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                0xff
            };

            serialConnection.Send(buffer);
        }
    }
}
