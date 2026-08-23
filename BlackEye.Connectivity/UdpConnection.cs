namespace BlackEye.Connectivity
{
    using System.Net;
    using System.Net.Sockets;

    public class UdpConnection : IConnection
    {
        private UdpClient udpClient = new UdpClient();

        private string hostname;

        private int serverPort;

        private int clientPort;

        public Action<byte[]> ReceivedCallback { private get; set; } = (b) => { };

        /// <summary>
        /// clientPort 0 lets the OS pick, which is what the captures show the
        /// reference client doing: the doc says the client may send from any port.
        /// Pinning one makes a second gateway on the same host fail to bind.
        /// </summary>
        public UdpConnection(string hostname, int clientPort = 0, int serverPort = 20001)
        {
            this.hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            this.clientPort = clientPort;
            this.serverPort = serverPort;
        }

        public void Connect()
        {
            udpClient = new UdpClient(clientPort);
            udpClient.BeginReceive(DataReceived, udpClient);
        }

        public void Close()
        {
            // Client is null once the UdpClient has been disposed, so a second
            // Close would otherwise throw.
            var socket = udpClient.Client;

            if (socket != null)
            {
                try
                {
                    // The socket is never connected - Send names its destination
                    // each time - so shutting it down reports ENOTCONN on some
                    // platforms.
                    socket.Shutdown(SocketShutdown.Receive);
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

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
            UdpClient? client = (UdpClient?)ar.AsyncState;

            try
            {
                IPEndPoint? receivedIpEndPoint = new IPEndPoint(IPAddress.Any, serverPort);
                byte[]? data = client?.EndReceive(ar, ref receivedIpEndPoint);

                if (data != null)
                {
                    this.ReceivedCallback?.Invoke(data);
                }
            }
            catch (ObjectDisposedException)
            {
                // The socket was closed; stop re-arming.
                return;
            }
            catch
            {
                // A malformed datagram, or a reader that threw on one, must not
                // stop us listening: the callback runs before BeginReceive is
                // re-armed, so an escaping exception would end reception for good.
            }

            try
            {
                client?.BeginReceive(DataReceived, ar.AsyncState);
            }
            catch (ObjectDisposedException)
            {
            }
        }

    }
}
