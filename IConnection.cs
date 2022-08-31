﻿namespace BlackEye
{
    internal interface IConnection
    {
        public void Connect();

        public void Close();

        void Write(byte[] data);

        Action<byte[]> ReceivedCallback { set; }
    }
}