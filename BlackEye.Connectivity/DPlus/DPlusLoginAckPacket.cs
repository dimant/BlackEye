namespace BlackEye.Connectivity.DPlus
{
    using System.Text;

    internal class DPlusLoginAckPacket : DPlusPacket
    {
        public bool Ack => "OKRW".SequenceEqual(Encoding.UTF8.GetString(buffer[4..8]));

        public DPlusLoginAckPacket(byte[] buffer) : base(buffer)
        {
        }
    }
}
