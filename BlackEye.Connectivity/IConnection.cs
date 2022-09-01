namespace BlackEye.Connectivity
{
    public interface IConnection
    {
        public void Connect();

        public void Close();

        void Send(byte[] data);

        Action<byte[]> ReceivedCallback { set; }
    }
}