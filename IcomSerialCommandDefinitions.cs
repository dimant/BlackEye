namespace BlackEye
{
    internal class IcomDef
    {
        public const byte DATA_TYPE_TERMINATE = 0xFF;

        public const byte TYPE_PONG = 0x03;

        public const byte TYPE_HEADER = 0x10;

        public const byte TYPE_DATA = 0x12;

        public const byte TYPE_HEADER_ACK = 0x21;

        public const byte TYPE_DATA_ACK = 0x23;

        public const uint RADIO_HEADER_LENGTH_BYTES = 41;

        public const uint VOICE_FRAME_LENGTH_BYTES = 9;

        public const uint DATA_FRAME_LENGTH_BYTES = 3;

        public const uint DV_FRAME_LENGTH_BYTES = VOICE_FRAME_LENGTH_BYTES + DATA_FRAME_LENGTH_BYTES;
    }
}
