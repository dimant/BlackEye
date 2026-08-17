namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using System;
    using System.Threading;
    using Xunit;

    /// <summary>
    /// PingHandler implements every keepalive in the app. Its timer interval is a
    /// hard-coded 1000 ms, so these tests are necessarily wall-clock bound; they
    /// poll for the expected effect rather than sleeping a fixed amount.
    /// </summary>
    public class PingHandlerTests
    {
        [Fact]
        public void NullActionsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new PingHandler(null!, () => { }, () => { }));
            Assert.Throws<ArgumentNullException>(() => new PingHandler(() => { }, null!, () => { }));
            Assert.Throws<ArgumentNullException>(() => new PingHandler(() => { }, () => { }, null!));
        }

        [Fact]
        public void PingsWhileThePongIsRecent()
        {
            int pings = 0;
            int timeOuts = 0;
            int errors = 0;

            var handler = new PingHandler(
                pingAction: () => Interlocked.Increment(ref pings),
                timeOutAction: () => Interlocked.Increment(ref timeOuts),
                errorAction: () => Interlocked.Increment(ref errors),
                maxPongWait: TimeSpan.FromSeconds(10));

            handler.Start();

            try
            {
                Assert.True(Wait.Until(() => Volatile.Read(ref pings) >= 1), "expected at least one ping");
            }
            finally
            {
                handler.Stop();
            }

            Assert.Equal(0, Volatile.Read(ref timeOuts));
            Assert.Equal(0, Volatile.Read(ref errors));
        }

        [Fact]
        public void TrafficSuppressesPingsEntirely()
        {
            int pings = 0;
            int timeOuts = 0;

            var handler = new PingHandler(
                pingAction: () => Interlocked.Increment(ref pings),
                timeOutAction: () => Interlocked.Increment(ref timeOuts),
                errorAction: () => { },
                maxPongWait: TimeSpan.FromMilliseconds(1200));

            handler.Start();

            try
            {
                // Pong restarts the 1000 ms timer, so traffic arriving every 300 ms
                // means it never elapses: the app only pings when the link is idle.
                for (int i = 0; i < 8; i++)
                {
                    Thread.Sleep(300);
                    handler.Pong();
                }
            }
            finally
            {
                handler.Stop();
            }

            Assert.Equal(0, Volatile.Read(ref pings));
            Assert.Equal(0, Volatile.Read(ref timeOuts));
        }

        [Fact]
        public void ThreeConsecutiveTimeoutsRaiseAnErrorAndStopTheTimer()
        {
            int pings = 0;
            int timeOuts = 0;
            int errors = 0;

            var handler = new PingHandler(
                pingAction: () => Interlocked.Increment(ref pings),
                timeOutAction: () => Interlocked.Increment(ref timeOuts),
                errorAction: () => Interlocked.Increment(ref errors),
                maxPongWait: TimeSpan.FromMilliseconds(1));

            handler.Start();

            try
            {
                Assert.True(Wait.Until(() => Volatile.Read(ref errors) >= 1, timeoutMs: 6000),
                    "expected the error action after three timeouts");
            }
            finally
            {
                handler.Stop();
            }

            // Two timeouts are reported, the third escalates to the error action.
            Assert.Equal(2, Volatile.Read(ref timeOuts));
            Assert.Equal(1, Volatile.Read(ref errors));
            Assert.Equal(0, Volatile.Read(ref pings));

            // The handler stopped itself, so nothing further fires.
            int errorsAtStop = Volatile.Read(ref errors);
            Thread.Sleep(1200);
            Assert.Equal(errorsAtStop, Volatile.Read(ref errors));
        }

        [Fact]
        public void MaxTimeOutsIsConfigurable()
        {
            int timeOuts = 0;
            int errors = 0;

            var handler = new PingHandler(
                pingAction: () => { },
                timeOutAction: () => Interlocked.Increment(ref timeOuts),
                errorAction: () => Interlocked.Increment(ref errors),
                maxPongWait: TimeSpan.FromMilliseconds(1),
                maxTimeOuts: 1);

            handler.Start();

            try
            {
                Assert.True(Wait.Until(() => Volatile.Read(ref errors) >= 1, timeoutMs: 4000),
                    "expected the first timeout to escalate immediately");
            }
            finally
            {
                handler.Stop();
            }

            Assert.Equal(0, Volatile.Read(ref timeOuts));
        }
    }
}
