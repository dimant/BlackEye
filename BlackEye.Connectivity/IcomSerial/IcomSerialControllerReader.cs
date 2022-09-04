namespace BlackEye.Connectivity.IcomSerial
{
    using System;
    using System.Threading.Tasks.Dataflow;

    public class IcomSerialControllerReader : BlockDataReceiver
    {
        private IControllerListener controllerListener;

        public IcomSerialControllerReader(IControllerListener controllerListener)
        {
            this.controllerListener = controllerListener ?? throw new ArgumentNullException(nameof(controllerListener));
        }

        public override void Receive(BufferBlock<byte> block)
        {
            var b = block.Receive();

            if (b == 0xFF)
            {
                return;
            }

            var len = block.Receive();
            var buffer = new byte[len];

            for (int i = 0; i < len; i++)
            {
                buffer[i] = block.Receive();
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
