namespace BlackEye
{
    internal interface IControllerListener
    {
        public void OnPong();

        public void OnHeader(byte[] header);

        public void OnHeaderAck();

        public void OnHeaderNak();

        public void OnData(byte[] data);

        public void OnDataAck(byte seqNumber);

        public void OnDataNak(byte seqNumber);

        public void OnEot();
    }
}
