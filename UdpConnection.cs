namespace BlackEye
{
    using System.Net;
    using System.Net.Sockets;

    public class UdpConnection : IConnection
    {
        private UdpClient udpClient = new UdpClient();

        private string hostname;

        private int port = 20001;

        public Action<byte[]> ReceivedCallback { private get; set; } = (b) => { };

        public UdpConnection(string hostname)
        {
            this.hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        }

        public void Connect()
        {
            udpClient = new UdpClient();
            udpClient.Connect(hostname, port);
            udpClient.BeginReceive(ReceiveData, null);
        }

        public void Close()
        {
            udpClient.Client.Shutdown(SocketShutdown.Receive);
            udpClient.Close();
        }

        public void Write(byte[] data)
        {
            udpClient?.Send(data, data.Length);

            // handle exceptions
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            UdpClient? udpClient = asyncResult.AsyncState as UdpClient;
            IPEndPoint? endPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] response = udpClient?.EndReceive(asyncResult, ref endPoint) ?? new byte[0];

            this.ReceivedCallback(response);
        }
    }
}
