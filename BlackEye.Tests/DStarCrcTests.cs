namespace BlackEye.Tests
{
    using BlackEye.Connectivity;
    using BlackEye.Connectivity.IcomTerminal;
    using Xunit;

    /// <summary>
    /// The D-STAR RF header carries a CRC over its 3 flag bytes plus the 4
    /// callsign fields: 39 bytes in total. The algorithm here is verified against
    /// the ID52's own captured header, which is the only capture that contains a
    /// real CRC - the DPlus reference client emits a constant instead.
    /// </summary>
    public class DStarCrcTests
    {
        [Fact]
        public void ComputeMatchesTheRadiosOwnHeaderCrc()
        {
            // Wire bytes 2..40: 3 flags plus 36 callsign bytes.
            var rfHeader = CaptureBytes.IcomHeaderFromRadioWire[2..41];

            var crc = DStarCrc.Compute(rfHeader, 0, rfHeader.Length);

            Assert.Equal(0x1458, crc);

            // The radio transmits the low byte first, at wire 41 then 42.
            Assert.Equal(0x58, (byte)(crc & 0xFF));
            Assert.Equal(0x14, (byte)(crc >> 8));
            Assert.Equal(CaptureBytes.IcomHeaderFromRadioWire[41], (byte)(crc & 0xFF));
            Assert.Equal(CaptureBytes.IcomHeaderFromRadioWire[42], (byte)(crc >> 8));
        }

        [Fact]
        public void ChangingAnyHeaderByteChangesTheCrc()
        {
            var rfHeader = CaptureBytes.IcomHeaderFromRadioWire[2..41];
            var baseline = DStarCrc.Compute(rfHeader, 0, rfHeader.Length);

            for (int i = 0; i < rfHeader.Length; i++)
            {
                var mutated = (byte[])rfHeader.Clone();
                mutated[i] ^= 0x01;

                Assert.NotEqual(baseline, DStarCrc.Compute(mutated, 0, mutated.Length));
            }
        }

        [Fact]
        public void ComputeHonoursTheOffsetAndCount()
        {
            var padded = new byte[3 + 39 + 3];
            CaptureBytes.IcomHeaderFromRadioWire[2..41].CopyTo(padded, 3);
            padded[0] = 0xAA;
            padded[^1] = 0xBB;

            Assert.Equal(0x1458, DStarCrc.Compute(padded, 3, 39));
        }

        [Fact]
        public void TheIcomHeaderExposesItsCrcAndRxStatus()
        {
            var header = new IcomTerminalHeader(CaptureBytes.Stripped(CaptureBytes.IcomHeaderFromRadioWire));

            Assert.Equal(0x1458, header.Crc);
            Assert.Equal(0x00, header.RxStatus);
            Assert.True(header.IsCrcValid());
        }

        [Fact]
        public void ACorruptedIcomHeaderFailsItsCrcCheck()
        {
            var wire = (byte[])CaptureBytes.IcomHeaderFromRadioWire.Clone();
            wire[29] = (byte)'B';   // first byte of mycall

            var header = new IcomTerminalHeader(CaptureBytes.Stripped(wire));

            Assert.False(header.IsCrcValid());
        }

        [Fact]
        public void TheReferenceClientsDPlusHeaderCrcIsNotACrcOfItsOwnContents()
        {
            // Documents why the captured 00 0b must not be copied into new
            // headers: it does not describe the header it travels with.
            var rfHeader = CaptureBytes.DPlusHeader[17..56];

            var crc = DStarCrc.Compute(rfHeader, 0, rfHeader.Length);

            Assert.Equal(0xe3, (byte)(crc & 0xFF));
            Assert.Equal(0x94, (byte)(crc >> 8));
            Assert.NotEqual(CaptureBytes.DPlusHeader[56], (byte)(crc & 0xFF));
        }
    }
}
