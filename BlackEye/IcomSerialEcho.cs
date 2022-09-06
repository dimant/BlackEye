﻿namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomSerial;
    using System.Timers;

    public class IcomSerialEcho : IControllerListener
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

        private IcomSerialControllerWriter writer;

        private StateType state = StateType.Receiving;

        private Queue<IcomSerialPacket> outputQueue = new Queue<IcomSerialPacket>();

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

        private void EchoHeader()
        {
            var packet = outputQueue.Peek();
            if (packet is IcomSerialHeader)
            {
                writer.SendHeader("AI6VW  L", "AI6VW  G", "AI6VW   ", "AI6VW  L", "    ");
            }
        }

        private void EchoFrame()
        {
            var packet = outputQueue.Peek();
            if (packet is IcomSerialFrame)
            {
                var frame = (IcomSerialFrame)packet;
                if (frame.IsLast())
                {
                    outputQueue.Dequeue();
                    writer.SendFrameEot();
                    state = StateType.Receiving;
                }
                else
                {
                    writer.SendFrame(frame.PacketId, frame.AmbeAndData);
                }
            }
        }

        public void OnFrame(IcomSerialFrame framePacket)
        {
            ResetPong();

            if (state == StateType.Receiving)
            {
                outputQueue.Enqueue(framePacket);

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
                    outputQueue.Dequeue();
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
                outputQueue = new Queue<IcomSerialPacket>();
                outputQueue.Enqueue(headerPacket);
            }
        }

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
        {
            ResetPong();

            if (state == StateType.TransmittingHeader)
            {
                outputQueue.Dequeue();
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
                writer.SendReset();
            }
            else
            {
                writer.SendPing();
            }
        }
    }
}
