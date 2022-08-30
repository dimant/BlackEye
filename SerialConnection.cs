namespace BlackEye
{
    using System.IO.Ports;

    internal class SerialConnection : ISerialConnection
    {
        private SerialPort serialPort;

        private Action<byte[]> dataReceivedCallback;

        private CancellationTokenSource cancellationTokenSource;

        public int BaudRate => this.serialPort.BaudRate;

        public SerialConnection(
            string portName,
            Action<byte[]> dataReceivedCallback,
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

            this.dataReceivedCallback = dataReceivedCallback;

            this.cancellationTokenSource = cancellationTokenSource;
        }

        public void Open()
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
                        this.dataReceivedCallback?.Invoke(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
                    }

                    this.dataReceivedCallback?.Invoke(buffer);
                }
            }
        }
    }
}
