namespace BlackEye.Connectivity.IcomSerial
{
    using System;
    using System.Threading.Tasks.Dataflow;

    public class IcomSerialReader : BlockDataReceiver
    {
        private ISerialListener controllerListener;

        public byte lastByte = 0x00;

        public byte currentByte = 0x00;

        public IcomSerialReader(ISerialListener controllerListener)
        {
            this.controllerListener = controllerListener ?? throw new ArgumentNullException(nameof(controllerListener));
        }

        private byte ReceiveContext(BufferBlock<byte> block)
        {
            lastByte = currentByte;
            currentByte = block.Receive();
            return currentByte;
        }

        public override void Receive(BufferBlock<byte> block)
        {
            byte len = 0;

            while (!(lastByte == 0xff && currentByte != 0xff))
            {
                len = ReceiveContext(block);
            }

            var buffer = new byte[len];

            for (int i = 0; i < len; i++)
            {
                buffer[i] = ReceiveContext(block);
            }

            var type = (IcomSerialPacket.PacketType)buffer[0];

            switch (type)
            {
                case IcomSerialPacket.PacketType.Pong:
                    var pongPacket = new IcomSerialPong(buffer);
                    if (pongPacket.IsValid())
                    {
                        this.controllerListener.OnPong(pongPacket);
                    }
                    break;
                case IcomSerialPacket.PacketType.HeaderFromSerial:
                    var headerPacket = new IcomSerialHeader(buffer);
                    if (headerPacket.IsValid())
                    {
                        this.controllerListener.OnHeader(headerPacket);
                    }
                    break;
                case IcomSerialPacket.PacketType.HeaderToSerialAck:
                    var headerAckPacket = new IcomSerialHeaderAck(buffer);
                    if (headerAckPacket.IsValid())
                    {
                        this.controllerListener.OnHeaderAck(headerAckPacket);
                    }
                    break;
                case IcomSerialPacket.PacketType.FrameFromSerial:
                    var framePacket = new IcomSerialFrame(buffer);
                    if (framePacket.IsValid())
                    {
                        this.controllerListener.OnFrame(framePacket);
                    }
                    break;
                case IcomSerialPacket.PacketType.FrameToSerialAck:
                    var frameAckPacket = new IcomSerialFrameAck(buffer);
                    if (frameAckPacket.IsValid())
                    {
                        this.controllerListener.OnFrameAck(frameAckPacket);
                    }
                    break;
            }
        }
    }
}
