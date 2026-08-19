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

        private ConcurrentQueue<QueuedFrame> terminalConnectionQueue = new ConcurrentQueue<QueuedFrame>();

        /// <summary>
        /// A radio-bound frame waiting for the radio to ack the previous one. The
        /// sequence id and number are stamped when the frame is actually sent, not
        /// here, so that filler frames advance the same counters.
        /// </summary>
        private record QueuedFrame(byte[]? AmbeAndData, bool IsEot);

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

            private const int MaxEmptyFrames = 100;

            private int emptyFrames = 0;

            private byte[]? lastHeaderPacket = null;

            // Radio-bound counters. They live here because this is the class that
            // writes to the serial port, on the radio's frame acks.
            private byte txSequenceId = 0;

            private byte txNumber = 0;

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

            /// <summary>
            /// Called when a stream starts from the network, before its header goes
            /// out to the radio.
            /// </summary>
            public void BeginReceiveStream()
            {
                txSequenceId = 0;
                txNumber = 0;
                emptyFrames = 0;

                dplusHandler.terminalConnectionQueue.Clear();
            }

            private void AdvanceTxCounters()
            {
                txSequenceId++;                                          // wraps at 255
                txNumber = (byte)(txNumber >= 20 ? 0 : txNumber + 1);    // 0..20
            }

            private void ReceiveFrame()
            {
                Thread.Sleep(FrameSleepMs);

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {
                    byte[] buffer;

                    if (dplusHandler.terminalConnectionQueue.TryDequeue(out var queued))
                    {
                        emptyFrames = 0;

                        if (queued.IsEot)
                        {
                            SendAndGoIdle(dplusHandler.terminalWriter.WriteFrameEot(txSequenceId, txNumber));
                            return;
                        }

                        buffer = dplusHandler.terminalWriter.WriteFrame(txSequenceId, txNumber, queued.AmbeAndData!);
                    }
                    else
                    {
                        emptyFrames++;

                        if (emptyFrames > MaxEmptyFrames)
                        {
                            // Give up on the network. The doc: the empty voice last
                            // frame is followed by the end of transmission frame.
                            dplusHandler.terminalConnection.Send(
                                dplusHandler.terminalWriter.WriteEmptyVoiceLastFrame(txSequenceId, txNumber));

                            AdvanceTxCounters();

                            SendAndGoIdle(dplusHandler.terminalWriter.WriteFrameEot(txSequenceId, txNumber));
                            return;
                        }

                        buffer = txNumber == 0
                            ? dplusHandler.terminalWriter.WriteEmptyVoiceSyncData(txSequenceId, txNumber)
                            : dplusHandler.terminalWriter.WriteEmptyVoiceEmptyData(txSequenceId, txNumber);
                    }

                    dplusHandler.terminalConnection.Send(buffer);

                    AdvanceTxCounters();
                });
            }

            private void SendAndGoIdle(byte[] buffer)
            {
                dplusHandler.terminalConnection.Send(buffer);

                emptyFrames = 0;

                // LockedState uses a Monitor, which is reentrant on this thread, so
                // transitioning from inside the enclosing CompareExecute is safe.
                dplusHandler.state.ExchangeExecute(TransceiverState_Idle, () => { });
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
                        var buffer = dplusHandler.networkWriter.WriteFrame(framePacket.AmbeAndData, sessionId, packetId);

                        dplusHandler.udpConnection.Send(buffer);

                        // The packet id counts 0..20 and then wraps.
                        packetId = (byte)(packetId >= 20 ? 0 : packetId + 1);

                        // Doc step 8: UDP drops headers, so resend on every wrap.
                        if (packetId == 0 && lastHeaderPacket != null)
                        {
                            for (int i = 0; i < headerSends; i++)
                            {
                                dplusHandler.udpConnection.Send(lastHeaderPacket);
                            }
                        }
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

                    lastHeaderPacket = dplusHandler.networkWriter.WriteHeader(
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
                    dplusHandler.terminalToDPlus.BeginReceiveStream();

                    var buffer = dplusHandler.terminalWriter.WriteHeader(
                        packet.Rpt1,
                        packet.Rpt2,
                        packet.UrCall,
                        packet.MyCall,
                        packet.Suffix);

                    dplusHandler.terminalConnection.Send(buffer);
                });
            }

            public void OnFrame(DPlusFramePacket packet)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                {
                    // Only the payload is queued: the ids are stamped when the frame
                    // is actually sent. The state changes to Idle when the end of
                    // transmission has gone out to the radio, not when it is queued.
                    dplusHandler.terminalConnectionQueue.Enqueue(
                        packet.IsLast()
                            ? new QueuedFrame(null, true)
                            : new QueuedFrame(packet.AmbeAndData, false));
                });
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
