namespace BlackEye
{
    internal abstract class IcomSerialCommand
    {
        private const int bufferSize = 2048;

        private byte[] buffer = new byte[bufferSize];

        private int pointer = 0;

        public void Consume(byte b)
        {
            buffer[pointer++] = b;


        }
    }
}
