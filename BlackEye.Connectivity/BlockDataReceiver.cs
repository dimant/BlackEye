namespace BlackEye.Connectivity
{
    using System.Threading.Tasks.Dataflow;

    public abstract class BlockDataReceiver
    {
        private BufferBlock<byte> block = new BufferBlock<byte>();

        public void OnReceived(byte[] data)
        {
            foreach (var b in data)
            {
                block.Post(b);
            }
        }

        public Task Start(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Receive(block);
                }
            }, cancellationToken);
        }

        public abstract void Receive(BufferBlock<byte> block);
    }
}
