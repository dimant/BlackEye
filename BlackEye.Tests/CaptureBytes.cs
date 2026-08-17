namespace BlackEye.Tests
{
    /// <summary>
    /// Bytes transcribed verbatim from dumps/. These are ground truth: if a test
    /// disagrees with one of these arrays, the code is wrong, not the array.
    ///
    /// Icom arrays are named *Wire because they include the leading length byte.
    /// IcomTerminalReader strips it before constructing a packet, so pass them
    /// through <see cref="Stripped"/> when building packet objects.
    /// </summary>
    public static class CaptureBytes
    {
        // ---------------------------------------------------------------
        // Icom terminal mode - rs-ms3w-rs232-dump.txt
        // ---------------------------------------------------------------

        /// <summary>Ping, computer to radio.</summary>
        public static readonly byte[] IcomPingWire = { 0x02, 0x02, 0xff };

        /// <summary>Pong, radio ready to send to the computer.</summary>
        public static readonly byte[] IcomPongWire = { 0x03, 0x03, 0x00, 0xff };

        /// <summary>Pong with flag 0x01: radio ready to receive frames.</summary>
        public static readonly byte[] IcomPongGoAheadWire = { 0x03, 0x03, 0x01, 0xff };

        /// <summary>Header ack from the radio.</summary>
        public static readonly byte[] IcomHeaderAckWire = { 0x03, 0x21, 0x00, 0xff };

        /// <summary>Frame ack from the radio for sequence id 7, status ready.</summary>
        public static readonly byte[] IcomFrameAckWire = { 0x04, 0x23, 0x07, 0x00, 0xff };

        /// <summary>
        /// Header from the transceiver, wire bytes 0..44. In terminal mode the
        /// radio puts DIRECT in both repeater fields. CRC 58 14 at wire 41,42
        /// (low byte first); rx status 00 at 43; terminator ff at 44.
        /// </summary>
        public static readonly byte[] IcomHeaderFromRadioWire =
        {
            0x2c, 0x10, 0x00, 0x00, 0x00, 0x44, 0x49, 0x52,
            0x45, 0x43, 0x54, 0x20, 0x20, 0x44, 0x49, 0x52,
            0x45, 0x43, 0x54, 0x20, 0x20, 0x43, 0x51, 0x43,
            0x51, 0x43, 0x51, 0x20, 0x20, 0x41, 0x49, 0x36,
            0x56, 0x57, 0x20, 0x20, 0x20, 0x49, 0x44, 0x35,
            0x32, 0x58, 0x14, 0x00, 0xff
        };

        /// <summary>First voice frame after the header: sequence 0, number 0, slow data sync.</summary>
        public static readonly byte[] IcomFrameFromRadioWire =
        {
            0x10, 0x12, 0x00, 0x00,
            0xb2, 0x4d, 0x22, 0x48, 0xc0, 0x16, 0x28, 0x26, 0xc8,
            0x55, 0x2d, 0x16,
            0xff
        };

        /// <summary>Second voice frame: sequence 1, number 1.</summary>
        public static readonly byte[] IcomSecondFrameFromRadioWire =
        {
            0x10, 0x12, 0x01, 0x01,
            0x12, 0x50, 0x0c, 0x56, 0x8e, 0x8b, 0xc8, 0xd2, 0xde,
            0x25, 0x4f, 0x93,
            0xff
        };

        /// <summary>
        /// The radio's terminator frame. Byte 3 = 0x42: last-frame bit 0x40 set,
        /// number 2. Payload is 55 c8 7a followed by nine zero bytes.
        /// </summary>
        public static readonly byte[] IcomEotFrameFromRadioWire =
        {
            0x10, 0x12, 0x56, 0x42,
            0x55, 0xc8, 0x7a, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00,
            0xff
        };

        /// <summary>Header to the transceiver, wire bytes 0..41. No CRC, no rx status.</summary>
        public static readonly byte[] IcomHeaderToRadioWire =
        {
            0x29, 0x20, 0x01, 0x00, 0x00, 0x41, 0x49, 0x36,
            0x56, 0x57, 0x20, 0x20, 0x4c, 0x41, 0x49, 0x36,
            0x56, 0x57, 0x20, 0x20, 0x47, 0x41, 0x49, 0x36,
            0x56, 0x57, 0x20, 0x20, 0x20, 0x41, 0x49, 0x36,
            0x56, 0x57, 0x20, 0x20, 0x47, 0x20, 0x20, 0x20,
            0x20, 0xff
        };

        /// <summary>
        /// The filler frame both reference applications actually send when there
        /// is no network audio to relay. Note the 16 29 f5 slow data tail:
        /// 97 cb e5, which IcomTerminalMode.md documents, appears in no capture.
        /// </summary>
        public static readonly byte[] IcomEmptyVoiceEmptyDataWire =
        {
            0x10, 0x22, 0x00, 0x00,
            0x9e, 0x8d, 0x32, 0x88, 0x26, 0x1a, 0x3f, 0x61, 0xe8,
            0x16, 0x29, 0xf5,
            0xff
        };

        /// <summary>End of transmission frame to the radio, ids continuing 07 07.</summary>
        public static readonly byte[] IcomFrameEotWire =
        {
            0x10, 0x22, 0x08, 0x48,
            0x55, 0xc8, 0x7a,
            0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
            0xff
        };

        /// <summary>The 9 AMBE bytes of a silent voice frame, common to both protocols.</summary>
        public static readonly byte[] SilentAmbe =
        {
            0x9e, 0x8d, 0x32, 0x88, 0x26, 0x1a, 0x3f, 0x61, 0xe8
        };

        // ---------------------------------------------------------------
        // DPlus - doozy-udp-dplus-dump.txt
        // ---------------------------------------------------------------

        /// <summary>Connect request; the server echoes it back verbatim.</summary>
        public static readonly byte[] DPlusConnect = { 0x05, 0x00, 0x18, 0x00, 0x01 };

        /// <summary>Disconnect: same packet with the last byte cleared.</summary>
        public static readonly byte[] DPlusDisconnect = { 0x05, 0x00, 0x18, 0x00, 0x00 };

        /// <summary>Ping and pong are the same three bytes in both directions.</summary>
        public static readonly byte[] DPlusPing = { 0x03, 0x60, 0x00 };

        /// <summary>Login, 28 bytes, mycall at 4..11 and 'DV019994' at 20..27.</summary>
        public static readonly byte[] DPlusLogin =
        {
            0x1c, 0xc0, 0x04, 0x00, 0x41, 0x49, 0x36, 0x56,
            0x57, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x44, 0x56, 0x30, 0x31,
            0x39, 0x39, 0x39, 0x34
        };

        /// <summary>Successful login response.</summary>
        public static readonly byte[] DPlusLoginAck =
        {
            0x08, 0xc0, 0x04, 0x00, (byte)'O', (byte)'K', (byte)'R', (byte)'W'
        };

        /// <summary>Rejected login response.</summary>
        public static readonly byte[] DPlusLoginNak =
        {
            0x08, 0xc0, 0x04, 0x00, (byte)'B', (byte)'U', (byte)'S', (byte)'Y'
        };

        /// <summary>End of transmission ack: session id at 4..5, frame count at 6.</summary>
        public static readonly byte[] DPlusEotAck =
        {
            0x0a, 0xc0, 0x0b, 0x00, 0x7d, 0x37, 0x3e, 0x00, 0x00, 0x00
        };

        /// <summary>
        /// Header, 58 bytes. The trailing 00 0b is the reference client's constant
        /// and is NOT a CRC of this header's contents (that would be e3 94).
        /// </summary>
        public static readonly byte[] DPlusHeader =
        {
            0x3a, 0x80, 0x44, 0x53, 0x56, 0x54, 0x10, 0x00,
            0x00, 0x00, 0x20, 0x00, 0x02, 0x01, 0x7d, 0x37,
            0x80, 0x00, 0x00, 0x00, 0x52, 0x45, 0x46, 0x30,
            0x33, 0x30, 0x20, 0x43, 0x41, 0x49, 0x36, 0x56,
            0x57, 0x20, 0x20, 0x44, 0x43, 0x51, 0x43, 0x51,
            0x43, 0x51, 0x20, 0x20, 0x41, 0x49, 0x36, 0x56,
            0x57, 0x20, 0x20, 0x20, 0x49, 0x44, 0x35, 0x32,
            0x00, 0x0b
        };

        /// <summary>
        /// The last voice frame of a stream, 29 bytes. Packet id 0x11 at byte 16,
        /// payload at 17..28, slow data 55 55 55 marking end of stream.
        /// </summary>
        public static readonly byte[] DPlusFrame =
        {
            0x1d, 0x80, 0x44, 0x53, 0x56, 0x54, 0x20, 0x00,
            0x00, 0x00, 0x20, 0x00, 0x02, 0x01, 0x7d, 0x37,
            0x11, 0x5b, 0x61, 0x94, 0x4b, 0xd4, 0xe3, 0xe0,
            0xa0, 0x6a, 0x55, 0x55, 0x55
        };

        /// <summary>
        /// The terminating frame, 32 bytes. Packet id 0x52 = 0x40 | 18, so the
        /// last-frame bit is set and the stream's count continues.
        /// </summary>
        public static readonly byte[] DPlusFrameEot =
        {
            0x20, 0x80, 0x44, 0x53, 0x56, 0x54, 0x20, 0x00,
            0x00, 0x00, 0x20, 0x00, 0x02, 0x01, 0x7d, 0x37,
            0x52, 0x9e, 0x8d, 0x32, 0x88, 0x26, 0x1a, 0x3f,
            0x61, 0xe8, 0x55, 0x55, 0x55, 0x55, 0xc8, 0x7a
        };

        /// <summary>
        /// Drops the leading length byte, matching what IcomTerminalReader hands
        /// to the packet classes: code buffer index = wire index - 1.
        /// </summary>
        public static byte[] Stripped(byte[] wire) => wire[1..];

        /// <summary>
        /// An ordinary DPlus voice frame with the given packet id, with slow data
        /// that is not the 55 55 55 end-of-stream marker.
        /// </summary>
        public static byte[] DPlusVoiceFrame(byte packetId)
        {
            var frame = (byte[])DPlusFrame.Clone();
            frame[16] = packetId;
            frame[26] = 0x16;
            frame[27] = 0x29;
            frame[28] = 0xf5;

            return frame;
        }
    }
}
