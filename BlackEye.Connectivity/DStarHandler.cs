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
