﻿namespace BlackEye.Connectivity
{
    using System.Threading;
    using System.Threading.Tasks.Dataflow;

    public class DPlusReader : BlockDataReceiver
    {
        private IGatewayListener gatewayListener;

        public DPlusReader(IGatewayListener gatewayListener)
        {
            this.gatewayListener = gatewayListener ?? throw new ArgumentNullException(nameof(gatewayListener));
        }
        public override void Receive(BufferBlock<byte> block, CancellationToken cancellationToken)
        {

        }

        public void ParseMessage(byte[] message, IGatewayListener controllerListener)
        {

        }
    }
}