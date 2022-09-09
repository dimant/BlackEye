namespace BlackEye.Connectivity.DPlus
{
    internal interface IDPlusListener
    {
        public void OnLoginAck(DPlusLoginAckPacket dPlusLoginAckPacket);

        public void OnHeader(DPlusHeaderPacket dPlusHeaderPacket);

        public void OnFrame(DPlusFramePacket dPlusFramePacket);

        public void OnDisconnectAck();

        public void OnPong();
    }
}
