namespace BlackEye.Tests
{
    using BlackEye;
    using BlackEye.Connectivity.IcomTerminal;
    using Xunit;

    /// <summary>
    /// IcomTerminalEcho is the only path currently reachable from Main: it records a
    /// transmission from the radio and plays it straight back, which is how the
    /// terminal protocol was validated in the first place.
    ///
    /// The tests drive the listener callbacks directly rather than calling Start(),
    /// whose returned task never completes.
    /// </summary>
    public class IcomTerminalEchoTests
    {
        private static IcomTerminalHeader Header() =>
            new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

        private static IcomTerminalFrame Frame(byte[] wire) =>
            new IcomTerminalFrame(CaptureBytes.Stripped(wire));

        private static IcomTerminalHeaderAck HeaderAck() =>
            new IcomTerminalHeaderAck(CaptureBytes.Stripped(CaptureBytes.IcomHeaderAckWire));

        private static IcomTerminalFrameAck FrameAck() =>
            new IcomTerminalFrameAck(CaptureBytes.Stripped(CaptureBytes.IcomFrameAckWire));

        private static IcomTerminalPong GoAhead() =>
            new IcomTerminalPong(CaptureBytes.Stripped(CaptureBytes.IcomPongGoAheadWire));

        [Fact]
        public void NothingIsSentWhileTheTransmissionIsStillBeingRecorded()
        {
            var serial = new FakeConnection();
            var echo = new IcomTerminalEcho(new IcomTerminalWriter(), serial);

            echo.OnHeader(Header());
            echo.OnFrame(Frame(CaptureBytes.IcomFrameFromRadioWire));
            echo.OnFrame(Frame(CaptureBytes.IcomSecondFrameFromRadioWire));

            Assert.Empty(serial.Sent);
        }

        [Fact]
        public void ARecordedTransmissionIsPlayedBackHeaderThenFramesThenEot()
        {
            var serial = new FakeConnection();
            var echo = new IcomTerminalEcho(new IcomTerminalWriter(), serial);

            echo.OnHeader(Header());
            echo.OnFrame(Frame(CaptureBytes.IcomFrameFromRadioWire));
            echo.OnFrame(Frame(CaptureBytes.IcomSecondFrameFromRadioWire));

            // The radio's terminator frame ends the recording and starts playback.
            // (This callback sleeps a second before echoing the header.)
            echo.OnFrame(Frame(CaptureBytes.IcomEotFrameFromRadioWire));

            var header = Assert.Single(serial.Sent);
            Assert.Equal(42, header.Length);
            Assert.Equal(0x20, header[1]);

            // Rpt1 sits at wire 5 and Rpt2 at 13, the order rs-ms3w uses in
            // dumps/rs-ms3w-rs232-dump.txt line 119.
            Assert.Equal("AI6VW  L", System.Text.Encoding.UTF8.GetString(header[5..13]));
            Assert.Equal("AI6VW  G", System.Text.Encoding.UTF8.GetString(header[13..21]));

            echo.OnHeaderAck(HeaderAck());
            echo.OnPong(GoAhead());

            // The radio acks each frame; only then is the next one sent.
            echo.OnFrameAck(FrameAck());
            echo.OnFrameAck(FrameAck());

            var sent = serial.Sent;
            Assert.Equal(4, sent.Count);

            var first = sent[1];
            var second = sent[2];
            var eot = sent[3];

            // Frames are replayed with the ids the radio originally used.
            Assert.Equal(0x22, first[1]);
            Assert.Equal(0x00, first[2]);
            Assert.Equal(0x00, first[3]);
            Assert.Equal(CaptureBytes.IcomFrameFromRadioWire[4..16], first[4..16]);

            Assert.Equal(0x01, second[2]);
            Assert.Equal(0x01, second[3]);
            Assert.Equal(CaptureBytes.IcomSecondFrameFromRadioWire[4..16], second[4..16]);

            // The terminator frame is not replayed as audio; it becomes an EOT that
            // continues the ids of the frames just echoed, as 07 07 -> 08 48 does
            // in the capture.
            Assert.Equal(0x22, eot[1]);
            Assert.Equal(0x02, eot[2]);
            Assert.Equal(0x42, eot[3]);
            Assert.Equal(0x40, eot[3] & 0x40);
            Assert.Equal(new byte[] { 0x55, 0xc8, 0x7a }, eot[4..7]);
        }

        [Fact]
        public void ARejectedHeaderDoesNotAdvancePlayback()
        {
            var serial = new FakeConnection();
            var echo = new IcomTerminalEcho(new IcomTerminalWriter(), serial);

            echo.OnHeader(Header());
            echo.OnFrame(Frame(CaptureBytes.IcomFrameFromRadioWire));
            echo.OnFrame(Frame(CaptureBytes.IcomEotFrameFromRadioWire));
            serial.Clear();

            // A nak must not be mistaken for an acceptance.
            echo.OnHeaderAck(new IcomTerminalHeaderAck(CaptureBytes.Stripped(new byte[] { 0x03, 0x21, 0x01, 0xff })));
            echo.OnPong(GoAhead());

            Assert.Empty(serial.Sent);
        }

        [Fact]
        public void AFrameAckArrivingBeforePlaybackStartsIsIgnored()
        {
            var serial = new FakeConnection();
            var echo = new IcomTerminalEcho(new IcomTerminalWriter(), serial);

            echo.OnHeader(Header());
            echo.OnFrame(Frame(CaptureBytes.IcomFrameFromRadioWire));

            // Still recording: a stray ack must not pull anything off the queue.
            echo.OnFrameAck(FrameAck());

            Assert.Empty(serial.Sent);
        }
    }
}
