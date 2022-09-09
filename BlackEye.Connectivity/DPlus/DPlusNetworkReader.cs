namespace BlackEye.Connectivity.DPlus
{
    public class DPlusNetworkReader
    {
        private IDPlusListener dPlusListener;

        public DPlusNetworkReader(IDPlusListener dPlusListener)
        {
            this.dPlusListener = dPlusListener ?? throw new ArgumentNullException(nameof(dPlusListener));
        }

        public void Receive(byte[] buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length < 2)
            {
                throw new ArgumentException($"{nameof(buffer)} is too short.");
            }

            if (buffer.Length != buffer[0])
            {
                throw new ArgumentException($"{nameof(buffer)}[0] does not match {nameof(buffer)}.Length");
            }

            var type = buffer[1];

            switch (type)
            {
                case 0xC0:
                    if (buffer[2] == 0x04)
                    {
                        var packet = new DPlusLoginAckPacket(buffer);

                        dPlusListener.OnLoginAck(packet);
                    }
                    else if (buffer[2] == 0x0b)
                    {
                        // ack disconnect
                        var tag = new byte[] { 0x0A, 0xC0, 0x0B, 0x00, 0x7D, 0x37, 0x3E, 0x00, 0x00, 0x00 };

                        var ack = buffer.SequenceEqual(tag);

                        dPlusListener.OnDisconnectAck();
                    }
                    break;
                case 0x60:
                    var pong = new byte[] { 0x03, 0x060, 0x00 };

                    var valid = buffer.SequenceEqual(pong);

                    dPlusListener.OnPong();
                    break;
                case 0x80:
                    if (buffer[6] == 0x10)
                    {
                        var packet = new DPlusHeaderPacket(buffer);

                        dPlusListener.OnHeader(packet);
                    }
                    else if (buffer[6] == 0x20)
                    {
                        var packet = new DPlusFramePacket(buffer);

                        dPlusListener.OnFrame(packet);
                    }
                    break;
            }
        }
    }
}
