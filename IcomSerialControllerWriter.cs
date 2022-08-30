namespace BlackEye
{
    internal class IcomSerialControllerWriter
    {
        private ISerialConnection serialConnection;

        public IcomSerialControllerWriter(ISerialConnection serialConnection)
        {
            this.serialConnection = serialConnection ?? throw new ArgumentNullException(nameof(serialConnection));
        }

        public void Ping()
        {
            byte[] cmd = new byte[]
            {
                0x02, 0x02, IcomDef.DATA_TYPE_TERMINATE
            };

            this.serialConnection.Write(cmd);
        }

        public void Poll()
        {
            byte[] cmd = new byte[]
            {
                IcomDef.DATA_TYPE_TERMINATE,
                IcomDef.DATA_TYPE_TERMINATE,
                IcomDef.DATA_TYPE_TERMINATE
            };

            this.serialConnection.Write(cmd);
        }
    }
}
