namespace BlackEye.Connectivity
{
    using System.Net;
    using System.Net.Sockets;

    public class UdpConnection : IConnection
    {
        private UdpClient udpClient = new UdpClient();

        private string hostname;

        private int port = 20001;

        public Action<byte[]> ReceivedCallback { private get; set; } = (b) => { };

        private IPEndPoint anyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public UdpConnection(string hostname)
        {
            this.hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        }

        public void Connect()
        {
            udpClient = new UdpClient();
            udpClient.Client.SendTimeout = 1000;
            udpClient.Client.ReceiveTimeout = 1000;
            udpClient.Connect(hostname, port);
            Run();
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

        private Task Run()
        {
            return Task.Run(() =>
            {
                while(true)
                {
                    try
                    {
                        var response = udpClient.Receive(ref anyEndPoint);
                        this.ReceivedCallback?.Invoke(response);
                    }
                    catch (SocketException e)
                    {
                        if (e.ErrorCode != 10060)
                        {
                            // Handle the error. 10060 is a timeout error, which is expected.
                            return;
                        }
                    }
                }
            });
        }
    }
}
