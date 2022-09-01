namespace BlackEye.Connectivity
{
    using System.Net;
    using System.Net.Sockets;

    public class UdpConnection : IConnection
    {
        private UdpClient udpClient = new UdpClient();

        private string hostname;

        private int serverPort = 20001;

        private int clientPort = 20002;

        public Action<byte[]> ReceivedCallback { private get; set; } = (b) => { };

        public UdpConnection(string hostname)
        {
            this.hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        }

        public void Connect()
        {
            udpClient = new UdpClient(clientPort);
            udpClient.BeginReceive(DataReceived, udpClient);
        }

        public void Close()
        {
            udpClient.Client.Shutdown(SocketShutdown.Receive);
            udpClient.Close();
        }

        public void Send(byte[] data)
        {
            try
            {
                udpClient?.Send(data, data.Length, hostname, serverPort);
            }
            catch
            {
                // handle exceptions
            }
        }

        private void DataReceived(IAsyncResult ar)
        {
            UdpClient? client = (UdpClient?) ar.AsyncState;
            IPEndPoint? receivedIpEndPoint = new IPEndPoint(IPAddress.Any, serverPort);
            byte[]? data = client?.EndReceive(ar, ref receivedIpEndPoint);

            if (data != null)
            {
                this.ReceivedCallback?.Invoke(data);
            }

            client?.BeginReceive(DataReceived, ar.AsyncState);
        }
    }
}
