namespace BlackEye.Connectivity
{
    public class DStarHandler
    {
        private class ControllerListenerInternal : IControllerListener
        {
            private DPlusWriter dplusWriter;

            public ControllerListenerInternal(DPlusWriter dplusWriter)
            {
                this.dplusWriter = dplusWriter ?? throw new ArgumentNullException(nameof(dplusWriter));
            }

            public void OnData(byte[] data)
            {
            }

            public void OnDataAck(byte seqNumber)
            {
            }

            public void OnDataNak(byte seqNumber)
            {
            }

            public void OnEot()
            {
            }

            public void OnHeader(byte[] header)
            {
            }

            public void OnHeaderAck()
            {
            }

            public void OnHeaderNak()
            {
            }

            public void OnPong()
            {
            }
        }

        private class GatewayListenerInternal : IGatewayListener
        {
            private IcomSerialControllerWriter icomWriter;

            public GatewayListenerInternal(IcomSerialControllerWriter icomWriter)
            {
                this.icomWriter = icomWriter ?? throw new ArgumentNullException(nameof(icomWriter));
            }
        }

        public IControllerListener ControllerListener { get; }

        public IGatewayListener GatewayListener { get; }

        public DStarHandler(IcomSerialControllerWriter icomWriter, DPlusWriter dplusWriter)
        {
            this.ControllerListener = new ControllerListenerInternal(dplusWriter);

            this.GatewayListener = new GatewayListenerInternal(icomWriter);
        }
    }
}
