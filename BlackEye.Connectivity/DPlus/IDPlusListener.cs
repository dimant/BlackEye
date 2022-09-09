namespace BlackEye.Connectivity.DPlus
{
    public interface IDPlusListener
    {
        public void OnConnectAck();

        public void OnLoginAck(DPlusLoginAckPacket dPlusLoginAckPacket);

        public void OnHeader(DPlusHeaderPacket dPlusHeaderPacket);

        public void OnFrame(DPlusFramePacket dPlusFramePacket);

        public void OnEotAck();

        public void OnPong();
    }
}
