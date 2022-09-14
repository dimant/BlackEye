namespace BlackEye
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.DPlus;
    using BlackEye.Connectivity.IcomSerial;
    using System.Collections.Concurrent;
    using System.ComponentModel.DataAnnotations;
    using System.Timers;

    /// <summary>
    /// If state is Idle:
    ///     TerminalToDPlus pings every 1000ms
    ///     DPlusToTerminal pongs on every ping
    ///
    /// If we get a header from the terminal and we're idle:
    ///     change state to transmitting
    ///     start writing frames to the udp connection as they come in from serial
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

        private const int TrasnceiverState_ReceivingHeader = 0x04;

        private const int TransceiverState_Transmitting = 0x05;

        private const int TransceiverState_Error = 0x06;

        private LockedState state = new LockedState(TransceiverState_Idle);

        private ConcurrentQueue<byte[]> serialConnectionQueue = new ConcurrentQueue<byte[]>();

        private DPlusNetworkWriter networkWriter;

        private IcomSerialWriter serialWriter;

        private IConnection serialConnection;

        private IConnection udpConnection;

        private object iolock = new object();

        private void Error()
        {
            state.ExchangeExecute(TransceiverState_Error, () =>
            {

            });
        }

        private class TerminalToDPlus : ISerialListener
        {
            private Random random = new Random();

            private DPlusHandler dplusHandler;

            private PingHandler pingHandler;

            private short sessionId = 0;

            private byte packetId = 0;

            private byte[]? lastHeaderPacket = null;

            private const int headerSends = 5;

            public TerminalToDPlus(DPlusHandler dplusHandler)
            {
                this.dplusHandler = dplusHandler ?? throw new ArgumentNullException(nameof(dplusHandler));
                
                var pingBytes = dplusHandler.serialWriter.WritePing();
                var resetBytes = dplusHandler.serialWriter.WriteReset();
                this.pingHandler = new PingHandler(
                    pingAction: () => dplusHandler.serialConnection.Send(pingBytes),
                    timeOutAction: () => dplusHandler.serialConnection.Send(resetBytes),
                    errorAction: () => dplusHandler.Error());
            }

            public void Start()
            {
                pingHandler.Start();
            }

            public void OnFrame(IcomSerialFrame framePacket)
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

            public void OnFrameAck(IcomSerialFrameAck frameAckPacket)
            {
                pingHandler.Pong();

                if (frameAckPacket.Ack)
                {
                    dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                    {
                        // try to dequeue from network receiving queue
                        // if we got something, send to transceiver
                        // else send empty to transceiver
                    });
                }
            }

            public void OnHeader(IcomSerialHeader headerPacket)
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

            public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket)
            {
                pingHandler.Pong();

                dplusHandler.state.CompareExchangeExecute(TrasnceiverState_ReceivingHeader, TransceiverState_Receiving, () =>
                {

                });
            }

            public void OnIgnore()
            {
                pingHandler.Pong();
            }

            public void OnPong(IcomSerialPong pongPacket)
            {
                pingHandler.Pong();

                if (pongPacket.PongType == IcomSerialPong.PongPacketType.Ack)
                {
                    dplusHandler.state.CompareExecute(TransceiverState_Receiving, () =>
                    {
                        // try to dequeue from network receiving queue
                        // if we got something, send to transceiver
                        // else send empty to transceiver
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
                    // send login
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
                    var buffer = dplusHandler.serialWriter.WriteHeader(
                        packet.Rpt2,
                        packet.Rpt1,
                        packet.UrCall,
                        packet.MyCall,
                        packet.Suffix);

                    dplusHandler.serialConnection.Send(buffer);
                });
            }

            public void OnFrame(DPlusFramePacket packet)
            {
                pingHandler.Pong();

                if (packet.IsLast())
                {
                    dplusHandler.state.CompareExchangeExecute(TransceiverState_Receiving, TransceiverState_Idle, () =>
                    {
                        var buffer = dplusHandler.serialWriter.WriteFrameEot();
                        dplusHandler.serialConnectionQueue.Enqueue(buffer);
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

                        var buffer = dplusHandler.serialWriter.WriteFrame(sequenceId, number, packet.AmbeAndData);
                        dplusHandler.serialConnectionQueue.Enqueue(buffer);
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
