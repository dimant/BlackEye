namespace BlackEye.Connectivity.DPlus
{
    public class DPlusFramePacket : DPlusPacket, IDStarFrame
    {
        // Byte 16 is the packet id; the 12 byte payload runs from 17 to 28. The
        // upper bounds are fixed rather than open ended because an end of
        // transmission frame is 32 bytes and its trailing 3 bytes are not payload.
        public byte[] Ambe => buffer[17..26];

        public byte[] AmbeAndData => buffer[17..29];

        public byte[] Data => buffer[26..29];

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
