namespace BlackEye.Connectivity.IcomSerial
{
    public interface IIcomSerialAck
    {
        bool Ack { get; }

        byte PacketId { get; }
    }
}