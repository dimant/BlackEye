namespace BlackEye.Connectivity.DPlus
{
    public class DPlusFramePacket : DPlusPacket, IDStarFrame
    {
        public byte[] Ambe => buffer[16..25];

        public byte[] AmbeAndData => buffer[16..];

        public byte[] Data => buffer[25..];

        public DPlusFramePacket(byte[] buffer) : base(buffer)
        {
        }

        public bool IsLast()
        {
            var tag = new byte[] { 0x55, 0x55, 0x55 };

            return Data.SequenceEqual(tag);
        }
    }
}
