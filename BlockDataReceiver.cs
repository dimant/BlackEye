namespace BlackEye
{
    using System.Threading.Tasks.Dataflow;

    internal abstract class BlockDataReceiver
    {
        private BufferBlock<byte> block = new BufferBlock<byte>();

        public void OnData(byte[] data)
        {
            foreach (var b in data)
            {
                block.Post(b);
            }
        }

        public Task Run(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Receive(block, cancellationToken);
                }
            }, cancellationToken);
        }

        public abstract void Receive(BufferBlock<byte> block, CancellationToken cancellationToken);
    }
}
