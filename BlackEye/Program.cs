namespace BlackEye
{
    using BlackEye.Connectivity;

    internal class Program
    {
        static void Main(string[] args)
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