namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomSerial;

    public class IcomSerialEcho : ISerialListener
    {
        private enum StateType
        {
            Receiving,
            TransmittingHeader,
            TransmittingFrames,
            Error
        };

        private IcomSerialWriter writer;

        private IConnection serialConnection;

        private PingHandler pingHandler;

        private StateType state = StateType.Receiving;

        private Queue<IcomSerialPacket> transceiverQueue = new Queue<IcomSerialPacket>();

        public IcomSerialEcho(IcomSerialWriter writer, IConnection serialConnection)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.serialConnection = serialConnection;

            this.pingHandler = new PingHandler(
            pingAction: () =>
            {
                var pingBytes = writer.WritePing();
                serialConnection.Send(pingBytes);
            },
            timeOutAction: () =>
            {
                var resetBytes = writer.WriteReset();
                serialConnection.Send(resetBytes);
            },
            errorAction: () => state = StateType.Error );
        }

        public Task Start()
        {
            var resetBytes = writer.WriteReset();

            for (int i = 0; i < 5; i++)
            {
                serialConnection.Send(resetBytes);
            }

            pingHandler.Start();

            return Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(250);
                };
            });
        }

        private void EchoHeader()
        {
            var packet = transceiverQueue.Peek();
            if (packet is IcomSerialHeader)
            {
                var headerBytes = writer.WriteHeader("AI6VW  L", "AI6VW  G", "AI6VW   ", "AI6VW  L", "    ");
                serialConnection.Send(headerBytes);
            }
        }

        private void EchoFrame()
        {
            var packet = transceiverQueue.Peek();
            if (packet is IcomSerialFrame)
            {
                var frame = (IcomSerialFrame)packet;
                if (frame.IsLast())
                {
                    transceiverQueue.Dequeue();
                    var eotBytes = writer.WriteFrameEot();
                    serialConnection.Send(eotBytes);
                    state = StateType.Receiving;
                }
                else
                {
                    var frameBytes = writer.WriteFrame(frame.PacketId, frame.AmbeAndData);
                    serialConnection.Send(frameBytes);
                }
            }
        }

        public void OnFrame(IcomSerialFrame framePacket)
        {
            pingHandler.Pong();

            if (state == StateType.Receiving)
            {
                transceiverQueue.Enqueue(framePacket);

                if (framePacket.IsLast())
                {
                    state = StateType.TransmittingHeader;
                    Thread.Sleep(1000);
                    EchoHeader();
                }
            }
        }

        public void OnFrameAck(IcomSerialFrameAck frameAckPacket)
        {
            pingHandler.Pong();

            if(state == StateType.TransmittingFrames)
            {
                if (frameAckPacket.Ack)
                {
                    transceiverQueue.Dequeue();
                    Thread.Sleep(12);
                    EchoFrame();
                }
            }
        }

        public void OnHeader(IcomSerialHeader headerPacket)
        {
            pingHandler.Pong();

            if (state == StateType.Receiving)
            {
                transceiverQueue = new Queue<IcomSerialPacket>();
                transceiverQueue.Enqueue(headerPacket);
            }
        }

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
        {
            pingHandler.Pong();

            if (state == StateType.TransmittingHeader)
            {
                transceiverQueue.Dequeue();
            }
            else
            {
                Thread.Sleep(50);
                EchoHeader();
            }
        }

        public void OnPong(IcomSerialPong pongPacket)
        {
            pingHandler.Pong();

            if (state == StateType.TransmittingHeader
                && pongPacket.PongType == IcomSerialPong.PongPacketType.Ack)
            {
                state = StateType.TransmittingFrames;
                EchoFrame();
            }
        }

        public void OnIgnore()
        {
            pingHandler.Pong();
        }
    }
}
