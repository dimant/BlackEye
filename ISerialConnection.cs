namespace BlackEye
{
    internal interface ISerialConnection
    {
        public int BaudRate { get; }

        void Write(byte[] data);
    }
}