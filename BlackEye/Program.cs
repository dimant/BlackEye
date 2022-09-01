namespace BlackEye
{
    using BlackEye.Connectivity;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    internal class Program
    {
        static void Main(string[] args)
        {
            var connection = new UdpConnection("localhost");

            connection.ReceivedCallback = (data) =>
            {

            };

            connection.Connect();

            // Send some test messages
            using (UdpClient sender1 = new UdpClient(20001))
                sender1.Send(Encoding.ASCII.GetBytes("Hi!"), 3, "localhost", 20002);
            using (UdpClient sender2 = new UdpClient(12345))
                sender2.Send(Encoding.ASCII.GetBytes("Hi!"), 3, "localhost", 20002);

            Console.ReadKey();
        }

        static void xxMain(string[] args)
        {
            var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;
            var serialConnection = new SerialConnection("COM4", cancellationSource);
            var icomWriter = new IcomSerialControllerWriter(serialConnection);

            var udpConnection = new UdpConnection("hostname");
            var dplusWriter = new DPlusWriter(udpConnection);

            var dstarHandler = new DStarHandler(icomWriter, dplusWriter);
            var icomReader = new IcomSerialControllerReader(dstarHandler.ControllerListener);
            var dplusReader = new DPlusReader(dstarHandler.GatewayListener);

            serialConnection.ReceivedCallback = icomReader.OnData;
            udpConnection.ReceivedCallback = dplusReader.OnData;

            try
            {
                icomReader.Run(cancellationToken);
                serialConnection.Connect();
            }
            catch
            {
                cancellationSource.Cancel();
            }

            while (true)
            {
                Thread.Sleep(500);
            }
        }
    }
}