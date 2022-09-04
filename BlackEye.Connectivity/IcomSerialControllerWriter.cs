using System.Reflection.PortableExecutable;

namespace BlackEye.Connectivity
{
    public class IcomSerialControllerWriter
    {
        private IConnection serialConnection;

        public IcomSerialControllerWriter(IConnection serialConnection)
        {
            this.serialConnection = serialConnection ?? throw new ArgumentNullException(nameof(serialConnection));
        }

        public void SendPing()
        {
            byte[] cmd = new byte[]
            {
                0x02, 0x02, IcomDef.DATA_TYPE_TERMINATE
            };

            this.serialConnection.Send(cmd);
        }

        public void SendPoll()
        {
            byte[] buffer = new byte[]
            {
                IcomDef.DATA_TYPE_TERMINATE,
                IcomDef.DATA_TYPE_TERMINATE,
                IcomDef.DATA_TYPE_TERMINATE
            };

            this.serialConnection.Send(buffer);
        }

        public void SendHeader(byte[] data)
        {
            byte[] buffer = new byte[42];
            buffer[0] = 41;    // len
            buffer[1] = 0x20;  // incoming header
            Buffer.BlockCopy(data, 0, buffer, 0, 37);
            buffer[41] = IcomDef.DATA_TYPE_TERMINATE;

            this.serialConnection.Send(buffer);
        }

        public void SendVoiceEot()
        {

        }
    }
}
