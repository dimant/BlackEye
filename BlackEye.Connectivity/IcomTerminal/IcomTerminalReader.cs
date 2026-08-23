namespace BlackEye.Connectivity.IcomTerminal
{
    using System;
    using System.Threading.Tasks.Dataflow;

    public class IcomTerminalReader : BlockDataReceiver
    {
        private ITerminalListener controllerListener;

        public byte lastByte = 0x00;

        public byte currentByte = 0x00;

        public IcomTerminalReader(ITerminalListener controllerListener)
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

            while (len == 0)
            {
                // A packet begins at the first non-terminator byte after a 0xff.
                while (!(lastByte == 0xff && currentByte != 0xff))
                {
                    len = ReceiveContext(block);
                }

                if (len == 0)
                {
                    // Resync landed on a stray 0x00. Indexing an empty buffer would
                    // throw on the background reader thread and end reception
                    // silently, so consume the byte and hunt for the next packet.
                    this.controllerListener.OnIgnore();

                    ReceiveContext(block);
                }
            }

            var buffer = new byte[len];

            for (int i = 0; i < len; i++)
            {
                buffer[i] = ReceiveContext(block);
            }

            var type = (IcomTerminalPacket.PacketType)buffer[0];

            switch (type)
            {
                case IcomTerminalPacket.PacketType.Pong:
                    var pongPacket = new IcomTerminalPong(buffer);
                    if (pongPacket.IsValid())
                    {
                        this.controllerListener.OnPong(pongPacket);
                    }
                    break;
                case IcomTerminalPacket.PacketType.HeaderFromSerial:
                    var headerPacket = new IcomTerminalHeader(buffer);
                    if (headerPacket.IsValid())
                    {
                        this.controllerListener.OnHeader(headerPacket);
                    }
                    break;
                case IcomTerminalPacket.PacketType.HeaderToSerialAck:
                    var headerAckPacket = new IcomTerminalHeaderAck(buffer);
                    if (headerAckPacket.IsValid())
                    {
                        this.controllerListener.OnHeaderAck(headerAckPacket);
                    }
                    break;
                case IcomTerminalPacket.PacketType.FrameFromSerial:
                    var framePacket = new IcomTerminalFrame(buffer);
                    if (framePacket.IsValid())
                    {
                        this.controllerListener.OnFrame(framePacket);
                    }
                    break;
                case IcomTerminalPacket.PacketType.FrameToSerialAck:
                    var frameAckPacket = new IcomTerminalFrameAck(buffer);
                    if (frameAckPacket.IsValid())
                    {
                        this.controllerListener.OnFrameAck(frameAckPacket);
                    }
                    break;
            }
        }
    }
}
