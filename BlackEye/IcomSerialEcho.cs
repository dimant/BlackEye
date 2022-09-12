namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomSerial;
    using System.Timers;

    public class IcomSerialEcho : ISerialListener
    {
        private enum StateType
        {
            Receiving,
            TransmittingHeader,
            TransmittingFrames
        };

        private DateTime lastPong = DateTime.Now;

        private readonly TimeSpan maxPongWait = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        private Timer pingTimer = new Timer();

        private IcomSerialWriter writer;

        private IConnection serialConnection;

        private StateType state = StateType.Receiving;

        private Queue<IcomSerialPacket> transceiverQueue = new Queue<IcomSerialPacket>();

        public IcomSerialEcho(IcomSerialWriter writer, IConnection serialConnection)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.serialConnection = serialConnection;
        }

        public Task Start()
        {
            pingTimer = new System.Timers.Timer(1000);
            pingTimer.Elapsed += Ping;
            pingTimer.Enabled = true;

            var resetBytes = writer.WriteReset();

            for (int i = 0; i < 5; i++)
            {
                serialConnection.Send(resetBytes);
            }

            var pingBytes = writer.WritePing();
            serialConnection.Send(pingBytes);

            return Task.Run(() =>
            {
                while (true) ;
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
            ResetPong();

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
            ResetPong();

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
            ResetPong();

            if (state == StateType.Receiving)
            {
                transceiverQueue = new Queue<IcomSerialPacket>();
                transceiverQueue.Enqueue(headerPacket);
            }
        }

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
        {
            ResetPong();

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
            ResetPong();

            if (state == StateType.TransmittingHeader
                && pongPacket.PongType == IcomSerialPong.PongPacketType.Ack)
            {
                state = StateType.TransmittingFrames;
                EchoFrame();
            }
        }

        public void OnIgnore()
        {
            ResetPong();
        }

        private void ResetPong()
        {
            lastPong = DateTime.Now;
            pingTimer.Stop();
            pingTimer.Start();
        }

        private void Ping(object? sender, ElapsedEventArgs e)
        {
            var delta = DateTime.Now - lastPong;
            if (delta > maxPongWait)
            {
                var resetBytes = writer.WriteReset();
                serialConnection.Send(resetBytes);
            }
            else
            {
                var pingBytes = writer.WritePing();
                serialConnection.Send(pingBytes);
            }
        }
    }
}
