namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomTerminal;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// An <see cref="IConnection"/> that records everything written to it and lets
    /// a test push bytes back through the receive callback.
    /// </summary>
    public class FakeConnection : IConnection
    {
        private readonly object sentLock = new object();

        private readonly List<byte[]> sent = new List<byte[]>();

        public Action<byte[]> ReceivedCallback { private get; set; } = b => { };

        public bool IsConnected { get; private set; }

        /// <summary>A snapshot of every buffer passed to Send, in order.</summary>
        public IReadOnlyList<byte[]> Sent
        {
            get
            {
                lock (sentLock)
                {
                    return sent.ToArray();
                }
            }
        }

        public void Connect() => IsConnected = true;

        public void Close() => IsConnected = false;

        public void Send(byte[] buffer)
        {
            lock (sentLock)
            {
                sent.Add(buffer);
            }
        }

        public void Clear()
        {
            lock (sentLock)
            {
                sent.Clear();
            }
        }

        /// <summary>Pushes bytes into whatever reader is wired to this connection.</summary>
        public void Receive(byte[] buffer) => ReceivedCallback.Invoke(buffer);
    }

    /// <summary>
    /// Records every callback an <see cref="IcomTerminalReader"/> dispatches.
    /// </summary>
    public class RecordingTerminalListener : ITerminalListener
    {
        public List<IcomTerminalPong> Pongs { get; } = new List<IcomTerminalPong>();

        public List<IcomTerminalHeader> Headers { get; } = new List<IcomTerminalHeader>();

        public List<IcomTerminalHeaderAck> HeaderAcks { get; } = new List<IcomTerminalHeaderAck>();

        public List<IcomTerminalFrame> Frames { get; } = new List<IcomTerminalFrame>();

        public List<IcomTerminalFrameAck> FrameAcks { get; } = new List<IcomTerminalFrameAck>();

        public int Ignores { get; private set; }

        public int TotalCallbacks =>
            Pongs.Count + Headers.Count + HeaderAcks.Count + Frames.Count + FrameAcks.Count + Ignores;

        public void OnPong(IcomTerminalPong pongPacket) => Pongs.Add(pongPacket);

        public void OnHeader(IcomTerminalHeader headerPacket) => Headers.Add(headerPacket);

        public void OnHeaderAck(IcomTerminalHeaderAck headerAckPacket) => HeaderAcks.Add(headerAckPacket);

        public void OnFrame(IcomTerminalFrame framePacket) => Frames.Add(framePacket);

        public void OnFrameAck(IcomTerminalFrameAck frameAckPacket) => FrameAcks.Add(frameAckPacket);

        public void OnIgnore() => Ignores++;
    }

    /// <summary>
    /// Polls a condition instead of sleeping a fixed amount, so timer-driven tests
    /// stay fast when things work and still fail rather than hang when they do not.
    /// </summary>
    public static class Wait
    {
        public static bool Until(Func<bool> condition, int timeoutMs = 3000, int pollMs = 10)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(pollMs);
            }

            return condition();
        }
    }
}
