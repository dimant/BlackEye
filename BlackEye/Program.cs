using BlackEye.Connectivity;
using BlackEye.Connectivity.IcomSerial;

namespace BlackEye
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var serialConnection = new SerialConnection("COM4", cancellationTokenSource);
            var icomWriter = new IcomSerialWriter(serialConnection);
            var echo = new IcomSerialEcho(icomWriter);
            var icomReader = new IcomSerialReader(echo);

            serialConnection.ReceivedCallback = icomReader.OnReceived;
            icomReader.Start(cancellationToken);
            serialConnection.Connect();
            echo.Start().Wait();
        }
    }
}