namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.DPlus;
    using BlackEye.Connectivity.IcomTerminal;
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// If state is Idle:
    ///     TerminalToDPlus pings every 1000ms
    ///     DPlusToTerminal pongs on every ping
    ///
    /// If we get a header from the terminal and we're idle:
    ///     change state to transmitting
    ///     start writing frames to the udp connection as they come in from terminal
    ///     on EOT switch back to idle
    ///
    /// If we get a header from DPlus and we're idle:
    ///     change state to receiving
    ///     clear icom queue
    ///     send header to terminal
    ///     on each ack, send the next frame
    ///     if no frame, send empty
    ///     if no frames for x ms or 100 frames, switch state to Idle
    ///     on EOT switch to Idle
    /// </summary>
    public class DPlusHandler
    {
        private const int TransceiverState_Disconnected = 0x01;

        private const int TransceiverState_Idle = 0x02;

        private const int TransceiverState_Receiving = 0x03;

        private const int TransceiverState_Transmitting = 0x04;

        private const int TransceiverState_Error = 0x05;

        private LockedState state = new LockedState(TransceiverState_Idle);

        private ConcurrentQueue<byte[]> terminalConnectionQueue = new ConcurrentQueue<byte[]>();

        private DPlusNetworkWriter networkWriter;

        private IcomTerminalWriter terminalWriter;

        private IConnection terminalConnection;

        private IConnection udpConnection;

        private TerminalToDPlus terminalToDPlus;

        private DPlusToTerminal dplusToTerminal;

        public ITerminalListener TerminalListener { get => terminalToDPlus; }

        public IDPlusListener DPlusListener { get => dplusToTerminal; }

        public DPlusHandler(
            DPlusNetworkWriter networkWriter,
            IcomTerminalWriter terminalWriter,
            IConnection terminalConnection,
            IConnection udpConnection)
        {
            this.networkWriter = networkWriter ?? throw new ArgumentNullException(nameof(networkWriter));
            this.terminalWriter = terminalWriter ?? throw new ArgumentNullException(nameof(terminalWriter));
            this.terminalConnection = terminalConnection ?? throw new ArgumentNullException(nameof(terminalConnection));
            this.udpConnection = udpConnection ?? throw new ArgumentNullException(nameof(udpConnection));

            this.terminalToDPlus = new TerminalToDPlus(this);
            this.dplusToTerminal = new DPlusToTerminal(this);
        }

        private void Error()
        {
            state.ExchangeExecute(TransceiverState_Error, () =>
            {

            });
        }

        private class TerminalToDPlus : ITerminalListener
        {
            private const int headerSends = 5;

            private const int FrameSleepMs = 12;

            private Random random = new Random();

            private DPlusHandler dplusHandler;

            private PingHandler pingHandler;

            private short sessionId = 0;

            private byte packetId = 0;

            private int emptyFrames = 0;

            private byte[]? lastHeaderPacket = null;

            public TerminalToDPlus(DPlusHandler dplusHandler)
            {
                this.dplusHandler = dplusHandler ?? throw new ArgumentNullException(nameof(dplusHandler));
                
                var pingBytes = dplusHandler.terminalWriter.WritePing();
                var resetBytes = dplusHandler.terminalWriter.WriteReset();
                this.pingHandler = new PingHandler(
                    pingAction: () => dplusHandler.terminalConnection.Send(pingBytes),
                    timeOutAction: () => dplusHandler.terminalConnection.Send(resetBytes),
                    errorAction: () => dplusHandler.Error());
            }

            public void Start()
            {
                pingHandler.Start();
            }

            private void ReceiveFrame()
            {
                byte[]? buffer;
                Thread.Sleep(FrameSleepMs);

                if (emptyFrames > 100)
                {
                    dplusHandler.state.CompareExchangeExecute(TransceiverState_Receiving, TransceiverState_Idle, () =>
                    {
                        buffer = dplusHandler.terminalWriter.WriteEmptyVoiceLastFrame();

                        dplusHandler.terminalConnection.Send(buffer);
                    });
                }

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {
                    if (!dplusHandler.terminalConnectionQueue.TryDequeue(out buffer))
                    {
                        emptyFrames++;

                        if (packetId == 0)
                        {
                            buffer = dplusHandler.terminalWriter.WriteEmptyVoiceSyncData();
                        }
                        else
                        {
                            buffer = dplusHandler.terminalWriter.WriteEmptyVoiceEmptyData();
                        }
                    }

                    dplusHandler.terminalConnection.Send(buffer);
                });
            }

            public void OnFrame(IcomTerminalFrame framePacket)
            {
                pingHandler.Pong();

                if (framePacket.IsLast())
                {
                    dplusHandler.state.CompareExchangeExecute(TransceiverState_Transmitting, TransceiverState_Idle, () =>
                    {
                        var buffer = dplusHandler.networkWriter.WriteFrameEot(sessionId, packetId);

                        dplusHandler.udpConnection.Send(buffer);
                    });
                }
                else
                {
                    dplusHandler.state.CompareExecute(TransceiverState_Transmitting, () =>
                    {
                        if (packetId >= 20)
                        {
                            packetId = 0;

                            if (lastHeaderPacket != null)
                            {
                                for (int i = 0; i < headerSends; i++)
                                {
                                    dplusHandler.udpConnection.Send(lastHeaderPacket);
                                }
                            }
                        }

                        var buffer = dplusHandler.networkWriter.WriteFrame(framePacket.AmbeAndData, sessionId, packetId);

                        dplusHandler.udpConnection.Send(buffer);
                    });
                }
            }

            public void OnFrameAck(IcomTerminalFrameAck frameAckPacket)
            {
                pingHandler.Pong();

                if (frameAckPacket.Ack)
                {
                    ReceiveFrame();
                }
            }

            public void OnHeader(IcomTerminalHeader headerPacket)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExchangeExecute(TransceiverState_Idle, TransceiverState_Transmitting, () =>
                {
                    sessionId = (short)random.Next(short.MaxValue);
                    packetId = 0;

                    var lastHeaderPacket = dplusHandler.networkWriter.WriteHeader(
                        headerPacket.Rpt1,
                        headerPacket.Rpt2,
                        headerPacket.UrCall,
                        headerPacket.MyCall,
                        headerPacket.Suffix,
                        sessionId);

                    for (int i = 0; i < headerSends; i++)
                    {
                        dplusHandler.udpConnection.Send(lastHeaderPacket);
                    }
                });
            }

            public void OnHeaderAck(IcomTerminalHeaderAck headerAckPacket)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {

                });
            }

            public void OnIgnore()
            {
                pingHandler.Pong();
            }

            public void OnPong(IcomTerminalPong pongPacket)
            {
                pingHandler.Pong();

                if (pongPacket.PongType == IcomTerminalPong.PongPacketType.Ack)
                {
                    dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                    {
                        ReceiveFrame();
                    });
                }
            }
        }

        private class DPlusToTerminal : IDPlusListener
        {
            private DPlusHandler dplusHandler;

            private PingHandler pingHandler;

            private byte sequenceId = 0;

            private byte number = 0;

            public DPlusToTerminal(DPlusHandler dplusHnadler)
            {
                this.dplusHandler = dplusHnadler ?? throw new ArgumentNullException(nameof(dplusHnadler));

                var pingBytes = dplusHnadler.networkWriter.WritePing();
                this.pingHandler = new PingHandler(
                    pingAction: () => dplusHandler.udpConnection.Send(pingBytes),
                    timeOutAction: () => dplusHandler.udpConnection.Send(pingBytes),
                    errorAction: () => dplusHandler.Error());
            }

            public void Start()
            {
            }

            public void OnConnectAck()
            {
                dplusHandler.state.CompareExecute(TransceiverState_Disconnected, () =>
                {
                    dplusHandler.networkWriter.WriteLogin("");
                });
            }

            public void OnLoginAck(DPlusLoginAckPacket packet)
            {
                if (packet.Ack)
                {
                    dplusHandler.state.CompareExchangeExecute(TransceiverState_Disconnected, TransceiverState_Idle, () =>
                    {
                        pingHandler.Start();
                    });
                }
            }

            public void OnHeader(DPlusHeaderPacket packet)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExchangeExecute(TransceiverState_Idle, TransceiverState_Receiving, () =>
                {
                    var buffer = dplusHandler.terminalWriter.WriteHeader(
                        packet.Rpt2,
                        packet.Rpt1,
                        packet.UrCall,
                        packet.MyCall,
                        packet.Suffix);

                    dplusHandler.terminalConnection.Send(buffer);
                });
            }

            public void OnFrame(DPlusFramePacket packet)
            {
                pingHandler.Pong();

                if (packet.IsLast())
                {
                    dplusHandler.state.CompareExchangeExecute(TransceiverState_Receiving, TransceiverState_Idle, () =>
                    {
                        var buffer = dplusHandler.terminalWriter.WriteFrameEot();
                        dplusHandler.terminalConnectionQueue.Enqueue(buffer);
                    });
                }
                else
                {
                    dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                    {
                        // bytes go from 0 to 0xFF (255 decimal)
                        sequenceId += 1;

                        number += 1;
                        if (number > 20)
                        {
                            number = 0;
                        }

                        var buffer = dplusHandler.terminalWriter.WriteFrame(sequenceId, number, packet.AmbeAndData);
                        dplusHandler.terminalConnectionQueue.Enqueue(buffer);
                    });
                }
            }

            public void OnEotAck()
            {
                pingHandler.Pong();
            }

            public void OnPong()
            {
                pingHandler.Pong();
            }
        }
    }
}
