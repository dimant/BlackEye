namespace BlackEye.Connectivity
{
    using System.Timers;

    public class PingHandler
    {
        private DateTime lastPong = DateTime.Now;

        private readonly TimeSpan maxPongWait;

        private System.Timers.Timer pingTimer = new System.Timers.Timer();

        private Action pingAction;

        private Action timeOutAction;

        private Action errorAction;

        private int maxTimeOuts;

        private int timeOuts = 0;

        public PingHandler(
            Action pingAction,
            Action timeOutAction,
            Action errorAction,
            TimeSpan? maxPongWait = null,
            int maxTimeOuts = 3)
        {
            this.pingAction = pingAction ?? throw new ArgumentNullException(nameof(pingAction));
            this.timeOutAction = timeOutAction ?? throw new ArgumentNullException(nameof(timeOutAction));
            this.errorAction = errorAction ?? throw new ArgumentNullException(nameof(errorAction));

            if (maxPongWait == null)
            {
                this.maxPongWait = new TimeSpan(hours: 0, minutes: 0, seconds: 5);
            }
            else
            {
                this.maxPongWait = maxPongWait.Value;
            }

            this.maxTimeOuts = maxTimeOuts;
        }

        public void Start()
        {
            pingTimer = new System.Timers.Timer(1000);
            pingTimer.Elapsed += Ping;
            pingTimer.Enabled = true;
        }

        public void Stop()
        {
            pingTimer.Stop();
            pingTimer.Enabled = false;
        }

        public void Pong()
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
                timeOuts++;

                if (timeOuts >= maxTimeOuts)
                {
                    errorAction();
                    Stop();
                }
                else
                {
                    timeOutAction();
                }
            }
            else
            {
                timeOuts = 0;
                pingAction();
            }
        }
    }
}
