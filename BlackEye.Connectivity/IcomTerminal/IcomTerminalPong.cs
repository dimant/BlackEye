﻿namespace BlackEye.Connectivity.IcomTerminal
{
    public class IcomTerminalPong : IcomTerminalPacket
    {
        public enum PongPacketType
        {
            Pong = 0x00,
            Ack = 0x01,
        }

        public PongPacketType PongType { get { return (PongPacketType)buffer[1]; } }

        public IcomTerminalPong(byte[] buffer) : base(buffer)
        {
        }

        public override bool IsValid()
        {
            if (!base.IsValid())
            {
                return false;
            }

            if (!((PacketType)buffer[0] == PacketType.Pong))
            {
                return false;
            }

            if (!Enum.IsDefined(typeof(PongPacketType), (int)buffer[1]))
            {
                return false;
            }

            return true;
        }
    }
}
