namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks.Dataflow;
    using Xunit;

    /// <summary>
    /// The serial side is a byte stream, so incoming data is funnelled into a
    /// BufferBlock and consumed by a background loop. This covers that plumbing
    /// independently of the Icom framing rules.
    /// </summary>
    public class BlockDataReceiverTests
    {
        private class ByteCollector : BlockDataReceiver
        {
            private readonly object gate = new object();

            private readonly List<byte> received = new List<byte>();

            public byte[] Received
            {
                get
                {
                    lock (gate)
                    {
                        return received.ToArray();
                    }
                }
            }

            public override void Receive(BufferBlock<byte> block)
            {
                var b = block.Receive();

                lock (gate)
                {
                    received.Add(b);
                }
            }
        }

        [Fact]
        public void BytesAreDeliveredInOrderAcrossChunkBoundaries()
        {
            var collector = new ByteCollector();
            var cancellation = new CancellationTokenSource();

            collector.Start(cancellation.Token);

            try
            {
                collector.OnReceived(new byte[] { 0x01, 0x02 });
                collector.OnReceived(new byte[] { 0x03 });
                collector.OnReceived(new byte[] { 0x04, 0x05, 0x06 });

                Assert.True(Wait.Until(() => collector.Received.Length == 6), "expected all six bytes");
                Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 }, collector.Received);
            }
            finally
            {
                cancellation.Cancel();
                collector.OnReceived(new byte[] { 0xFF });
            }
        }

        [Fact]
        public void AnEmptyChunkIsHarmless()
        {
            var collector = new ByteCollector();
            var cancellation = new CancellationTokenSource();

            collector.Start(cancellation.Token);

            try
            {
                collector.OnReceived(System.Array.Empty<byte>());
                collector.OnReceived(new byte[] { 0x2a });

                Assert.True(Wait.Until(() => collector.Received.Length == 1), "expected the single real byte");
                Assert.Equal(new byte[] { 0x2a }, collector.Received);
            }
            finally
            {
                cancellation.Cancel();
                collector.OnReceived(new byte[] { 0xFF });
            }
        }

        [Fact]
        public void CancellationStopsTheConsumerLoop()
        {
            var collector = new ByteCollector();
            var cancellation = new CancellationTokenSource();

            collector.Start(cancellation.Token);

            collector.OnReceived(new byte[] { 0x01 });
            Assert.True(Wait.Until(() => collector.Received.Length == 1), "expected the loop to be running");

            cancellation.Cancel();

            // The loop is parked in Receive(); one more byte lets it return and
            // observe the cancellation, after which nothing else is consumed.
            collector.OnReceived(new byte[] { 0x02 });
            Assert.True(Wait.Until(() => collector.Received.Length == 2), "expected the parked read to complete");

            collector.OnReceived(new byte[] { 0x03, 0x04, 0x05 });
            Thread.Sleep(100);

            Assert.Equal(2, collector.Received.Length);
            Assert.Equal(new byte[] { 0x01, 0x02 }, collector.Received);
        }
    }
}
