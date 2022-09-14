namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomTerminal;

    internal class Program
    {
        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            var serialConnection = new SerialConnection("COM4", cancellationTokenSource);
            var icomWriter = new IcomTerminalWriter();
            var echo = new IcomTerminalEcho(icomWriter, serialConnection);
            var icomReader = new IcomTerminalReader(echo);

            serialConnection.ReceivedCallback = icomReader.OnReceived;
            icomReader.Start(cancellationToken);
            serialConnection.Connect();
            echo.Start().Wait();
        }
    }
}