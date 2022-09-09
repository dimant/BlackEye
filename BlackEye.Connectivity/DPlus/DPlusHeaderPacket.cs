namespace BlackEye.Connectivity.DPlus
{
    using System.Text;

    internal class DPlusHeaderPacket : DPlusPacket, IDStarHeader
    {
        public string Rpt2 => Encoding.UTF8.GetString(buffer[20..28]);

        public string Rpt1 => Encoding.UTF8.GetString(buffer[28..36]);

        public string UrCall => Encoding.UTF8.GetString(buffer[36..44]);

        public string MyCall => Encoding.UTF8.GetString(buffer[44..52]);

        public string Suffix => Encoding.UTF8.GetString(buffer[52..56]);

        public DPlusHeaderPacket(byte[] buffer) : base(buffer)
        {
        }
    }
}
