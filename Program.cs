namespace BlackEye
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var dstarHandler = new DStarHandler();
            var cancellationSource = new CancellationTokenSource();
            var cancellationToken = cancellationSource.Token;
            var icomReader = new IcomSerialControllerReader(dstarHandler);
            var serialConnection = new SerialConnection("COM4", icomReader.OnData, cancellationSource);
            var icomWriter = new IcomSerialControllerWriter(serialConnection);

            try
            {
                icomReader.Run(cancellationToken);
                serialConnection.Open();

                icomWriter.Ping();
                icomWriter.Ping();
                icomWriter.Ping();
                icomWriter.Ping();

                while (true)
                {
                    Thread.Sleep(500);
                }
            }
            catch
            {
                cancellationSource.Cancel();
            }
        }
    }
}