namespace BlackEye.Connectivity
{
    /// <summary>
    /// CRC-16 X-25 (reflected CCITT: init 0xFFFF, polynomial 0x8408, final xor
    /// 0xFFFF) over the 39 byte D-STAR RF header - 3 flag bytes followed by rpt1,
    /// rpt2, urcall, mycall and the suffix.
    ///
    /// Verified against the ID52's own header in dumps/rs-ms3w-rs232-dump.txt,
    /// which carries 58 14 at wire bytes 41 and 42: the radio transmits the low
    /// byte first.
    /// </summary>
    public static class DStarCrc
    {
        public static ushort Compute(byte[] data, int offset, int count)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(offset)} and {nameof(count)} must describe a range inside {nameof(data)}.");
            }

            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i];

                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0
                        ? (ushort)((crc >> 1) ^ 0x8408)
                        : (ushort)(crc >> 1);
                }
            }

            return (ushort)(crc ^ 0xFFFF);
        }
    }
}
