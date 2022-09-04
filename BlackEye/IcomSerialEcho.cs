namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomSerial;
    using System.Timers;

    public class IcomSerialEcho : IControllerListener
    {
        private enum StateType
        {
            Receiving,
            Transmitting
        };

        private DateTime lastPong = DateTime.Now;

        private readonly TimeSpan maxPongWait = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        private Timer pingTimer = new Timer();

        private IcomSerialControllerWriter writer;

        private StateType state = StateType.Receiving;

        private Queue<IcomSerialPacket> echoQueue = new Queue<IcomSerialPacket>();

        public IcomSerialEcho(IcomSerialControllerWriter writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public Task Start()
        {
            pingTimer = new System.Timers.Timer(1000);
            pingTimer.Elapsed += Ping;
            pingTimer.Enabled = true;

            writer.SendReset();
            writer.SendPing();

            return Task.Run(() =>
            {
                while (true) ;
            });
        }

        private void EchoPacket()
        {
            Thread.Sleep(50);
            if (echoQueue.Count == 0)
            {
                state = StateType.Receiving;
                return;
            }
            else
            {
                var packet = echoQueue.Peek();

                if (packet is IcomSerialHeader)
                {
                    var header = (IcomSerialHeader)packet;
                    writer.SendHeader("AI6VW  L", "AI6VW  G", "AI6VW   ", "AI6VW  G", header.Suffix);
                }
                else if (packet is IcomSerialFrame)
                {
                    var frame = (IcomSerialFrame)packet;
                    if (frame.IsLast())
                    {
                        writer.SendFrameEot();
                    }
                    else
                    {
                        writer.SendFrame(frame.PacketId, frame.AmbeAndData);
                    }
                }
            }
        }

        public void OnFrame(IcomSerialFrame framePacket)
        {
            ResetPong();

            if (state == StateType.Receiving)
            {
                echoQueue.Enqueue(framePacket);

                if (framePacket.IsLast())
                {
                    state = StateType.Transmitting;
                    Thread.Sleep(1000);
                    EchoPacket();
                }
            }
        }

        public void OnFrameAck(IcomSerialFrameAck frameAckPacket)
        {
            ResetPong();

            if(state == StateType.Transmitting)
            {
                echoQueue.Dequeue();
                EchoPacket();
            }
        }

        public void OnHeader(IcomSerialHeader headerPacket)
        {
            if (state == StateType.Receiving)
            {
                echoQueue = new Queue<IcomSerialPacket>();
                echoQueue.Enqueue(headerPacket);
            }

            ResetPong();
        }

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
        {
            ResetPong();

            if (state == StateType.Transmitting)
            {
                echoQueue.Dequeue();
            }
        }

        public void OnPong(IcomSerialPong pongPacket)
        {
            ResetPong();

            if (state == StateType.Transmitting
                && pongPacket.PongType == IcomSerialPong.PongPacketType.Ack)
            {
                EchoPacket();
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
                writer.SendReset();
            }
            else
            {
                writer.SendPing();
            }
        }
    }
}
