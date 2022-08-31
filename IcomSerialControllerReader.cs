namespace BlackEye
{
    using System;
    using System.Threading.Tasks.Dataflow;

    internal class IcomSerialControllerReader : BlockDataReceiver
    {
        private IControllerListener controllerListener;

        public IcomSerialControllerReader(IControllerListener controllerListener)
        {
            this.controllerListener = controllerListener ?? throw new ArgumentNullException(nameof(controllerListener));
        }

        public override void Receive(BufferBlock<byte> block, CancellationToken cancellationToken)
        {
            var b = block.Receive();

            if (b == 0xFF)
            {
                return;
            }

            var len = b;

            if (len != 0x03 && len != 0x04 && len != 0x10 && len != 0x2C)
            {
                return;
            }

            var type = block.Receive();

            if (type != IcomDef.TYPE_PONG
                && type != IcomDef.TYPE_HEADER
                && type != IcomDef.TYPE_DATA
                && type != IcomDef.TYPE_HEADER_ACK
                && type != IcomDef.TYPE_DATA_ACK)
            {
                return;
            }

            byte[] message = new byte[len];
            message[0] = len;
            message[1] = type;
            for (int pointer = 2; pointer < len; pointer++)
            {
                message[pointer] = block.Receive();
            }

            ParseMessage(message, controllerListener);
        }

        public void ParseMessage(byte[] message, IControllerListener controllerListener)
        {
            switch (message[1])
            {
                case IcomDef.TYPE_PONG:
                    controllerListener.OnPong();
                    break;
                case IcomDef.TYPE_HEADER:
                    byte[] header = new byte[IcomDef.RADIO_HEADER_LENGTH_BYTES];
                    Array.Copy(message, 2, header, 0, IcomDef.RADIO_HEADER_LENGTH_BYTES);
                    controllerListener.OnHeader(header);
                    break;
                case IcomDef.TYPE_DATA:
                    if ((message[3] & 0x40) == 0x40)
                    {
                        controllerListener.OnEot();
                    }
                    else
                    {
                        byte[] data = new byte[IcomDef.DV_FRAME_LENGTH_BYTES];
                        Array.Copy(message, 2, data, 0, IcomDef.DV_FRAME_LENGTH_BYTES);
                        controllerListener.OnHeader(data);
                    }
                    break;
                case IcomDef.TYPE_HEADER_ACK:
                    if (message[2] == 0x00)
                    {
                        controllerListener.OnHeaderAck();
                    }
                    else
                    {
                        controllerListener.OnHeaderNak();
                    }
                    break;
                case IcomDef.TYPE_DATA_ACK:
                    if (message[3] == 0x00)
                    {
                        controllerListener.OnDataAck(message[2]);
                    }
                    else
                    {
                        controllerListener.OnDataNak(message[2]);
                    }
                    break;
            }
        }
    }
}
