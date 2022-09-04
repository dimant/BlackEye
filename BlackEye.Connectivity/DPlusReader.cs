namespace BlackEye.Connectivity
{
    using System.Threading.Tasks.Dataflow;

    public class DPlusReader
    {
        private const int LEN_PING = 30;

        private const int LEN_ACK = 5;

        private const int LEN_CONNECTED = 8;

        private const int LEN_SEEN_START = 10;

        private const int LEN_VOICE = 29;

        private const int LEN_VOICE_EOT = 32;

        private const int LEN_HEADER = 58;

        private IGatewayListener gatewayListener;

        public DPlusReader(IGatewayListener gatewayListener)
        {
            this.gatewayListener = gatewayListener ?? throw new ArgumentNullException(nameof(gatewayListener));
        }
        public void OnData(byte[] data)
        {
            ParseMessage(data, gatewayListener);
        }

        public void ParseMessage(byte[] message, IGatewayListener gatewayListener)
        {

        }
    }
}
