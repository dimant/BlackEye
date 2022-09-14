namespace BlackEye.Connectivity.IcomTerminal
{
    public interface ITerminalListener
    {
        public void OnPong(IcomTerminalPong pongPacket);

        public void OnHeader(IcomTerminalHeader headerPacket);

        public void OnHeaderAck(IcomTerminalHeaderAck headerAckPacket);

        public void OnFrame(IcomTerminalFrame framePacket);

        public void OnFrameAck(IcomTerminalFrameAck frameAckPacket);

        public void OnIgnore();
    }
}
