namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomSerial;
    using System.Timers;

    public class IcomSerialEcho : IControllerListener
    {
        private DateTime lastPong = DateTime.Now;

        private readonly TimeSpan maxPongWait = new TimeSpan(hours: 0, minutes: 0, seconds: 5);

        private System.Timers.Timer pingTimer = new System.Timers.Timer();

        private IcomSerialControllerWriter writer;

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

        public void OnFrame(IcomSerialFrame framePacket)
        {
            ResetPong();
        }

        public void OnFrameAck(IcomSerialFrameAck frameAckPacket)
        {
            ResetPong();
        }

        public void OnHeader(IcomSerialHeader headerPacket)
        {
            ResetPong();
        }

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
        {
            ResetPong();
        }

        public void OnPong(IcomSerialPong pongPacket)
        {
            ResetPong();
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
