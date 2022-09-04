using BlackEye.Connectivity.IcomSerial;

namespace BlackEye.Connectivity
{
    public interface IControllerListener
    {
        public void OnPong(IcomSerialPong pongPacket);

        public void OnHeader(IcomSerialHeader headerPacket);

        public void OnHeaderAck(IcomSerialHeaderAck headerAckPacket);

        public void OnFrame(IcomSerialFrame framePacket);

        public void OnFrameAck(IcomSerialFrameAck frameAckPacket);
    }
}
