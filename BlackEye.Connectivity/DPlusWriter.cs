namespace BlackEye.Connectivity
{
    using System;
    using System.Text;

    public class DPlusWriter
    {
        private IConnection udpConnection;

        public DPlusWriter(IConnection udpConnection)
        {
            this.udpConnection = udpConnection ?? throw new ArgumentNullException(nameof(udpConnection));
        }

        public void SendConnect()
        {
            byte[] data = new byte[5] { 0x05, 0x00, 0x18, 0x00, 0x01 };
            udpConnection.Send(data);
        }

        public void SendDisconnect()
        {
            byte[] data = new byte[5] { 0x05, 0x00, 0x18, 0x00, 0x00 };
            udpConnection.Send(data);
        }

        public void SendLogin(string myCall)
        {
            byte[] data = new byte[28]
            {
                0x1C, 0xC0, 0x04, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x44, 0x56, 0x30, 0x31, 0x39, 0x39, 0x39, 0x34
            };

            byte[] myCallBytes = Encoding.UTF8.GetBytes(myCall);
            Buffer.BlockCopy(myCallBytes, 0, data, 4, 8);

            udpConnection.Send(data);
        }

        public void SendKeepAlive()
        {
            byte[] data = new byte[3] { 0x03, 0x60, 0x00 };
            udpConnection.Send(data);
        }

        public void SendDvHeader(byte[] header, byte[] sessionId)
        {
            byte[] data = new byte[]
            {
                0x3A, 0x80, (byte)'D', (byte)'S', (byte)'V', (byte)'T',
                0x10,
                0x00, 0x00, 0x00,           // flags?
                0x10, 0x00, 0x02, 0x01,
                0x00, 0x00,                 // session id
                0x80,

            };

        }

        public void SendDvFrame(byte[] frame, byte[] sessionId, byte packetId)
        {

        }

        public void SendDvFrameLast(byte[] sessionId, byte packetId)
        {

        }
    }
}
