namespace BlackEye.Connectivity.IcomTerminal
{
    using System.Text;

    public class IcomTerminalWriter
    {
        public byte[] WritePing()
        {
            byte[] buffer = new byte[] { 0x02, 0x02, 0xff };

            return buffer;
        }

        /// <summary>
        /// Resyncs the radio by spamming the packet terminator. The doc calls for
        /// between 5 and 100 bytes; three was below the floor.
        /// </summary>
        public byte[] WriteReset()
        {
            var buffer = new byte[16];

            Array.Fill(buffer, (byte)0xff);

            return buffer;
        }

        public byte[] WriteHeader(string rpt1, string rpt2, string urcall, string mycall, string suffix)
        {
            var rpt1Bytes = Encoding.UTF8.GetBytes(rpt1);
            var rpt2Bytes = Encoding.UTF8.GetBytes(rpt2);
            var urcallBytes = Encoding.UTF8.GetBytes(urcall);
            var mycallBytes = Encoding.UTF8.GetBytes(mycall);
            var suffixBytes = Encoding.UTF8.GetBytes(suffix);

            var dstarHeader = new byte[36];
            rpt1Bytes.CopyTo(dstarHeader, 0);
            rpt2Bytes.CopyTo(dstarHeader, 8);
            urcallBytes.CopyTo(dstarHeader, 16);
            mycallBytes.CopyTo(dstarHeader, 24);
            suffixBytes.CopyTo(dstarHeader, 32);

            return WriteHeader(dstarHeader);
        }

        /// <summary>
        /// dstarHeader is 36 bytes ordered Rpt1, Rpt2, urcall, mycall (8 each) then
        /// suffix (4). NOTE: DPlusNetworkWriter's equivalent overload expects Rpt2
        /// first - the two protocols carry those two fields swapped, so the blobs
        /// are not interchangeable between the two writers.
        /// </summary>
        public byte[] WriteHeader(byte[] dstarHeader)
        {
            if (dstarHeader.Length != 36)
            {
                throw new ArgumentException($"{nameof(dstarHeader)} must be 36 bytes: rpt1, rpt2, urcall, mycall (8 each) then suffix (4).");
            }

            var tag = new byte[] { 0x29, 0x20, 0x01, 0x00, 0x00 };
            var buffer = new byte[42];
            tag.CopyTo(buffer, 0);
            dstarHeader.CopyTo(buffer, 5);
            buffer[41] = 0xff;

            return buffer;
        }

        public byte[] WriteFrame(byte sequenceId, byte number, byte[] ambeAndData)
        {
            if (ambeAndData.Length != 12)
            {
                throw new ArgumentException($"{nameof(ambeAndData)} must be 12 bytes. 9 bytes AMBE and 3 bytes data.");
            }

            var tag = new byte[] { 0x10, 0x22, sequenceId, number};
            var buffer = new byte[17];
            tag.CopyTo(buffer, 0);
            ambeAndData.CopyTo(buffer, 4);
            buffer[16] = 0xff;

            return buffer;
        }

        /// <summary>
        /// The terminating frame. Bit 0x40 on the number byte marks it as last: the
        /// capture shows 10 22 08 48 directly after 10 22 07 07, so the ids continue
        /// the transmission rather than restarting.
        /// </summary>
        public byte[] WriteFrameEot(byte sequenceId, byte number)
        {
            return new byte[17]
            {
                0x10, 0x22, sequenceId, (byte)(number | 0x40),
                0x55, 0xc8, 0x7a,
                0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
                0xff
            };
        }

        /// <summary>
        /// Sent when the radio acks a frame but no network audio has arrived. The
        /// 16 29 f5 slow data tail is what both reference applications send;
        /// 97 cb e5, which IcomTerminalMode.md documents, appears in no capture.
        /// </summary>
        public byte[] WriteEmptyVoiceEmptyData(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x16, 0x29, 0xf5);
        }

        /// <summary>
        /// Empty voice carrying the slow data sync pattern, sent at number 0.
        /// </summary>
        public byte[] WriteEmptyVoiceSyncData(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x55, 0x2d, 0x16);
        }

        /// <summary>
        /// Empty voice carrying the end of stream marker, sent when a transmission
        /// is being torn down. It must be followed by the frame from WriteFrameEot.
        /// </summary>
        public byte[] WriteEmptyVoiceLastFrame(byte sequenceId, byte number)
        {
            return WriteSpecialFrame(sequenceId, number, 0x55, 0x55, 0x55);
        }

        private byte[] WriteSpecialFrame(byte sequenceId, byte number, byte data0, byte data1, byte data2)
        {
            return new byte[17]
            {
                0x10, 0x22, sequenceId, number,
                0x9e, 0x8d, 0x32, 0x88, 0x26, 0x1a, 0x3f, 0x61, 0xe8,
                data0, data1, data2,
                0xff
            };
        }

    }
}
