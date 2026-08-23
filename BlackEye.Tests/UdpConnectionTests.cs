namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using Xunit;

    /// <summary>
    /// Exercises the real socket over loopback. The server port is injectable so a
    /// test can bind an ephemeral one and never collide with a running gateway.
    /// </summary>
    public class UdpConnectionTests : IDisposable
    {
        private readonly UdpClient server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

        private readonly List<UdpConnection> connections = new List<UdpConnection>();

        private int ServerPort => ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        private UdpConnection NewConnection()
        {
            var connection = new UdpConnection("127.0.0.1", clientPort: 0, serverPort: ServerPort);
            connections.Add(connection);

            return connection;
        }

        public void Dispose()
        {
            foreach (var connection in connections)
            {
                connection.Close();
            }

            server.Dispose();
        }

        [Fact]
        public void SendReachesTheServer()
        {
            var connection = NewConnection();
            connection.Connect();

            connection.Send(CaptureBytes.DPlusConnect);

            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            server.Client.ReceiveTimeout = 3000;
            var received = server.Receive(ref endpoint);

            Assert.Equal(CaptureBytes.DPlusConnect, received);
        }

        [Fact]
        public void DatagramsFromTheServerReachTheCallback()
        {
            var received = new List<byte[]>();
            var connection = NewConnection();
            connection.ReceivedCallback = b => { lock (received) { received.Add(b); } };
            connection.Connect();

            // Learn the client's ephemeral port by having it speak first.
            connection.Send(CaptureBytes.DPlusConnect);
            var client = new IPEndPoint(IPAddress.Any, 0);
            server.Client.ReceiveTimeout = 3000;
            server.Receive(ref client);

            server.Send(CaptureBytes.DPlusPing, CaptureBytes.DPlusPing.Length, client);

            Assert.True(Wait.Until(() => { lock (received) { return received.Count == 1; } }),
                "expected the datagram to reach the callback");
            Assert.Equal(CaptureBytes.DPlusPing, received[0]);
        }

        [Fact]
        public void ACallbackThatThrowsDoesNotKillTheReceiveLoop()
        {
            // DPlusNetworkReader throws on a datagram whose length byte disagrees
            // with its size. One of those must not take the socket down for good.
            var delivered = new List<byte[]>();
            var connection = NewConnection();
            connection.ReceivedCallback = b =>
            {
                lock (delivered) { delivered.Add(b); }

                if (b.Length == 1)
                {
                    throw new ArgumentException("malformed datagram");
                }
            };
            connection.Connect();

            connection.Send(CaptureBytes.DPlusConnect);
            var client = new IPEndPoint(IPAddress.Any, 0);
            server.Client.ReceiveTimeout = 3000;
            server.Receive(ref client);

            server.Send(new byte[] { 0x7f }, 1, client);
            Assert.True(Wait.Until(() => { lock (delivered) { return delivered.Count == 1; } }),
                "expected the malformed datagram to be delivered");

            server.Send(CaptureBytes.DPlusPing, CaptureBytes.DPlusPing.Length, client);
            Assert.True(Wait.Until(() => { lock (delivered) { return delivered.Count == 2; } }),
                "the receive loop died on the malformed datagram");
        }

        [Fact]
        public void TwoGatewaysCanRunSideBySide()
        {
            // A pinned client port would make the second bind throw.
            var first = NewConnection();
            var second = NewConnection();

            first.Connect();
            second.Connect();

            first.Send(CaptureBytes.DPlusPing);
            second.Send(CaptureBytes.DPlusPing);
        }

        [Fact]
        public void CloseIsSafeOnAConnectionThatOnlyEverSent()
        {
            var connection = NewConnection();
            connection.Connect();
            connection.Send(CaptureBytes.DPlusPing);

            connection.Close();
            connection.Close();
        }
    }
}
