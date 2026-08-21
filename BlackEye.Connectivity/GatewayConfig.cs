namespace BlackEye.Connectivity
{
    /// <summary>
    /// The identities the gateway puts on the air.
    ///
    /// In terminal mode the radio sends DIRECT in both of its repeater fields, but
    /// a DPlus reflector verifies mycall and rpt2 before letting a transmission
    /// through, so the gateway substitutes these before forwarding.
    /// </summary>
    public record GatewayConfig(
        string MyCall,
        string Suffix,
        string ReflectorCall,
        char ReflectorModule,
        char RadioModule)
    {
        public static GatewayConfig Default { get; } =
            new GatewayConfig("AI6VW", "ID52", "REF030", 'C', 'D');

        /// <summary>e.g. "AI6VW   " - 8 characters, space padded.</summary>
        public string PaddedMyCall => MyCall.PadRight(8);

        /// <summary>e.g. "AI6VW  D" - callsign in 7 characters, module in the 8th.</summary>
        public string Rpt1 => MyCall.PadRight(7) + RadioModule;

        /// <summary>e.g. "REF030 C" - reflector in 7 characters, module in the 8th.</summary>
        public string Rpt2 => ReflectorCall.PadRight(7) + ReflectorModule;

        public string UrCall => "CQCQCQ  ";

        public string PaddedSuffix => Suffix.PadRight(4);
    }
}
