namespace BlackEye.Connectivity
{
    using System;

    public class DPlusWriter
    {
        private IConnection udpConnection;

        public DPlusWriter(IConnection udpConnection)
        {
            this.udpConnection = udpConnection ?? throw new ArgumentNullException(nameof(udpConnection));
        }

        public void SendStart()
        {

        }

        public void SendDisconnect()
        {

        }

        public void SendMyCall(string myCall)
        {

        }

        public void SendPong()
        {

        }

        public void SendHeader(byte[] data)
        {

        }

        public void SendVoice(byte[] data)
        {

        }

        public void SendVoiceEot()
        {

        }
    }
}
