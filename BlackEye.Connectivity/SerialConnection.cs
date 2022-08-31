namespace BlackEye.Connectivity
{
    using System.IO.Ports;

    public class SerialConnection : IConnection
    {
        private SerialPort serialPort;

        public Action<byte[]> ReceivedCallback { private get; set; } = (b) => { };

        private CancellationTokenSource cancellationTokenSource;

        public int BaudRate => this.serialPort.BaudRate;

        public SerialConnection(
            string portName,
            CancellationTokenSource cancellationTokenSource)
        {
            this.serialPort = new SerialPort(portName)
            {
                PortName = portName,
                BaudRate = 19200,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                RtsEnable = false,
                ReadTimeout = 250,
                WriteTimeout = 250,
                WriteBufferSize = 2048,
            };

            this.serialPort.DataReceived += OnDataReceived;

            this.cancellationTokenSource = cancellationTokenSource;
        }

        public void Connect()
        {
            this.serialPort.Open();
        }

        public void Close()
        {
            this.serialPort.Close();
        }

        public void Write(byte[] data)
        {
            if (this.serialPort.IsOpen)
            {
                try
                {
                    this.serialPort.Write(data, 0, data.Length);
                }
                catch (TimeoutException)
                {
                    try
                    {
                        this.serialPort.Close();
                    }
                    catch (IOException)
                    {
                    }

                    cancellationTokenSource.Cancel();
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (this.serialPort.IsOpen)
            {
                int count = this.serialPort.BytesToRead;

                if (count > 0)
                {
                    byte[] buffer = new byte[count];

                    try
                    {
                        this.serialPort.Read(buffer, 0, count);
                    }
                    catch
                    {
                        cancellationTokenSource.Cancel();
                        this.ReceivedCallback.Invoke(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                    }

                    this.ReceivedCallback.Invoke(buffer);
                }
            }
        }
    }
}
