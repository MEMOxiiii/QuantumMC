namespace QuantumMC.Config
{
    public class ServerConfig
    {
        public string BindAddress { get; set; } = "0.0.0.0";
        public int Port { get; set; } = 19132;
        public int MaxPlayers { get; set; } = 20;

        public bool DebugMode { get; set; } = false;

        public string Motd { get; set; } = "QuantumMC Server";
        public string SubMotd { get; set; } = "Powered by QuantumMC";
        public int GameMode { get; set; } = 0;
        public bool XboxAuth { get; set; } = true;

        public string WorldName { get; set; } = "world";
        public string WorldGenerator { get; set; } = "flat";

        // TODO: Add more options later
    }
}
